using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Resources;

namespace SkeletonKey.Artifacts.FileSystem.Tests;

/// <summary>Covers atomic filesystem checkpoint persistence and integrity checks.</summary>
public sealed class FileSystemWorkflowCheckpointStoreTests
{
    /// <summary>Verifies a terminal checkpoint round-trips without losing result or port values.</summary>
    [Fact]
    public async Task SavesAndLoadsIntegrityProtectedCheckpoint()
    {
        string root = TemporaryRoot();
        try
        {
            FileSystemWorkflowCheckpointStore store = new(root);
            WorkflowExecutionCheckpoint checkpoint = Checkpoint(1, terminal: true);

            await store.SaveAsync(checkpoint, expectedRevision: 0);
            WorkflowExecutionCheckpoint loaded = Assert.IsType<WorkflowExecutionCheckpoint>(await store.LoadAsync("execution"));

            Assert.Equal(1, loaded.Revision);
            Assert.True(loaded.IsTerminal);
            Assert.Equal(WorkflowExecutionStatus.Succeeded, loaded.TerminalResult!.Status);
            Assert.Equal(42, loaded.Steps[0].Outputs[0].Values[0]!.GetValue<int>());
            Assert.Empty(loaded.Resources);
            Assert.Single(Directory.GetFiles(root, "checkpoint-*.json", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    /// <summary>Verifies provider-owned resource recovery state round-trips through format 0.3.</summary>
    [Fact]
    public async Task SavesAndLoadsRuntimeResourceState()
    {
        string root = TemporaryRoot();
        try
        {
            WorkflowExecutionCheckpoint source = Checkpoint(1, terminal: false);
            WorkflowExecutionCheckpoint checkpoint = new(
                source.FormatVersion,
                source.ExecutionId,
                source.WorkflowId,
                source.WorkflowSpecVersion,
                source.PlanId,
                source.RequestFingerprint,
                source.Revision,
                source.SavedAtUtc,
                source.IsTerminal,
                source.Steps,
                source.NodeActivationOrdinals,
                source.ExecutedAttempts,
                source.RuntimeActivations,
                source.Invocations,
                source.EventSequence,
                source.RecordsEmitted,
                source.ElapsedDurationMilliseconds,
                source.TerminalStatus,
                source.Outcome,
                source.Error,
                source.TerminalResult,
                source.NodeResults,
                source.NodeSnapshots,
                resources:
                [
                    new WorkflowCheckpointResource(
                        "page",
                        "web.page",
                        isResumable: true,
                        state: new WorkflowRuntimeResourceCheckpointState("0.1", new JsonObject { ["activePageId"] = "primary" })),
                ]);
            FileSystemWorkflowCheckpointStore store = new(root);

            await store.SaveAsync(checkpoint, expectedRevision: 0);
            WorkflowExecutionCheckpoint loaded = Assert.IsType<WorkflowExecutionCheckpoint>(await store.LoadAsync("execution"));

            WorkflowCheckpointResource resource = Assert.Single(loaded.Resources);
            Assert.Equal("page", resource.ResourceName);
            Assert.Equal("web.page", resource.Kind);
            Assert.True(resource.IsResumable);
            Assert.Equal("primary", resource.State!.Payload["activePageId"]!.GetValue<string>());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    /// <summary>Verifies an unavailable checkpoint root fails with the stable store-failure code.</summary>
    [Fact]
    [Trait("Category", "Phase30GA")]
    public void UnavailableCheckpointRootUsesStableStoreFailureCode()
    {
        string parent = TemporaryRoot();
        Directory.CreateDirectory(parent);
        string root = Path.Combine(parent, "blocked-root");
        File.WriteAllText(root, "blocked");
        try
        {
            WorkflowCheckpointStoreException exception = Assert.Throws<WorkflowCheckpointStoreException>(() => new FileSystemWorkflowCheckpointStore(root));

            Assert.Equal(WorkflowCheckpointErrorCodes.StoreFailure, exception.Code);
        }
        finally
        {
            DeleteRoot(parent);
        }
    }

    /// <summary>Verifies optimistic revision conflicts do not overwrite the current checkpoint.</summary>
    [Fact]
    public async Task RejectsRevisionConflictWithoutOverwrite()
    {
        string root = TemporaryRoot();
        try
        {
            FileSystemWorkflowCheckpointStore store = new(root);
            await store.SaveAsync(Checkpoint(1, terminal: false), expectedRevision: 0);

            WorkflowCheckpointStoreException exception = await Assert.ThrowsAsync<WorkflowCheckpointStoreException>(() => store.SaveAsync(Checkpoint(2, terminal: true), expectedRevision: 0).AsTask());

            Assert.Equal(WorkflowCheckpointErrorCodes.RevisionConflict, exception.Code);
            Assert.Equal(1, (await store.LoadAsync("execution"))!.Revision);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    /// <summary>Verifies payload tampering is detected before deserialization is trusted.</summary>
    [Fact]
    public async Task RejectsChecksumMismatch()
    {
        string root = TemporaryRoot();
        try
        {
            FileSystemWorkflowCheckpointStore store = new(root);
            await store.SaveAsync(Checkpoint(1, terminal: false), expectedRevision: 0);
            string path = Assert.Single(Directory.GetFiles(root, "checkpoint-*.json", SearchOption.TopDirectoryOnly));
            var envelope = (JsonObject)JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            envelope["sha256"] = new string('0', 64);
            await File.WriteAllTextAsync(path, envelope.ToJsonString());

            WorkflowCheckpointStoreException exception = await Assert.ThrowsAsync<WorkflowCheckpointStoreException>(() => store.LoadAsync("execution").AsTask());

            Assert.Equal(WorkflowCheckpointErrorCodes.InvalidCheckpoint, exception.Code);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    /// <summary>Verifies retry-safe boundary fields round-trip through the integrity-protected file format.</summary>
    [Fact]
    public async Task SavesAndLoadsRetryBoundaryMetadata()
    {
        string root = TemporaryRoot();
        try
        {
            DateTimeOffset notBefore = new(2026, 8, 17, 12, 0, 1, TimeSpan.Zero);
            WorkflowCheckpointStep step = new(
                "step:retry",
                "retry",
                "demo.retry",
                WorkflowStepRuntimeStatus.Ready,
                entryActivated: false,
                attempt: 1,
                resultStatus: NodeExecutionStatus.Failed,
                error: new WorkflowError("TEST-RETRY", "Expected failure."),
                retryAttempt: 1,
                retryNotBeforeUtc: notBefore);
            NodeExecutionResult nodeResult = new(
                "execution",
                "workflow",
                "invocation:execution:root",
                "retry",
                "demo.retry",
                NodeExecutionStatus.Failed,
                1,
                error: new WorkflowError("TEST-RETRY", "Expected failure."));
            WorkflowExecutionCheckpoint checkpoint = new(
                WorkflowExecutionCheckpoint.CurrentFormatVersion,
                "execution",
                "workflow",
                "0.1",
                "plan",
                new string('a', 64),
                revision: 1,
                savedAtUtc: new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero),
                isTerminal: false,
                steps: [step],
                nodeActivationOrdinals: new Dictionary<string, int>(StringComparer.Ordinal) { ["retry"] = 1 },
                executedAttempts: 1,
                runtimeActivations: 1,
                nodeResults: [nodeResult]);
            FileSystemWorkflowCheckpointStore store = new(root);

            await store.SaveAsync(checkpoint, expectedRevision: 0);
            WorkflowExecutionCheckpoint loaded = Assert.IsType<WorkflowExecutionCheckpoint>(await store.LoadAsync("execution"));

            Assert.Equal(1, loaded.Steps[0].RetryAttempt);
            Assert.Equal(notBefore, loaded.Steps[0].RetryNotBeforeUtc);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    /// <summary>Verifies the filesystem provider continues to accept the Phase 0-17 checkpoint envelope version.</summary>
    [Fact]
    public async Task SavesAndLoadsLegacyCheckpointVersion()
    {
        string root = TemporaryRoot();
        try
        {
            FileSystemWorkflowCheckpointStore store = new(root);
            WorkflowExecutionCheckpoint checkpoint = Checkpoint(1, terminal: false, formatVersion: WorkflowExecutionCheckpoint.LegacyFormatVersion);

            await store.SaveAsync(checkpoint, expectedRevision: 0);
            WorkflowExecutionCheckpoint loaded = Assert.IsType<WorkflowExecutionCheckpoint>(await store.LoadAsync("execution"));

            Assert.Equal(WorkflowExecutionCheckpoint.LegacyFormatVersion, loaded.FormatVersion);
            Assert.Equal(0, loaded.Steps[0].RetryAttempt);
            Assert.Null(loaded.Steps[0].RetryNotBeforeUtc);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static WorkflowExecutionCheckpoint Checkpoint(long revision, bool terminal, string? formatVersion = null)
    {
        WorkflowCheckpointStep step = new(
            "step:one",
            "one",
            "demo.one",
            WorkflowStepRuntimeStatus.Succeeded,
            entryActivated: true,
            activatedControlInputs: ["entry"],
            outputs: [new WorkflowCheckpointPortValue("value", [JsonValue.Create(42)])],
            attempt: 1,
            resultStatus: NodeExecutionStatus.Succeeded);
        WorkflowExecutionResult? result = terminal
            ? new WorkflowExecutionResult("execution", "workflow", "invocation:execution:root", null, WorkflowExecutionStatus.Succeeded, outputs: new Dictionary<string, JsonNode?> { ["value"] = JsonValue.Create(42) })
            : null;
        NodeExecutionResult nodeResult = new(
            "execution",
            "workflow",
            "invocation:execution:root",
            "one",
            "demo.one",
            NodeExecutionStatus.Succeeded,
            1,
            new Dictionary<string, JsonNode?> { ["value"] = JsonValue.Create(42) });
        return new WorkflowExecutionCheckpoint(
            formatVersion ?? WorkflowExecutionCheckpoint.CurrentFormatVersion,
            "execution",
            "workflow",
            "0.1",
            "plan",
            new string('a', 64),
            revision,
            new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero),
            terminal,
            [step],
            new Dictionary<string, int>(StringComparer.Ordinal) { ["one"] = 1 },
            executedAttempts: 1,
            runtimeActivations: 1,
            terminalResult: result,
            nodeResults: [nodeResult]);
    }

    private static string TemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), "skeletonkey-checkpoint-tests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
