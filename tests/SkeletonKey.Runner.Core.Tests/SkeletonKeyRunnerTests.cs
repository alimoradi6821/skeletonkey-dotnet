using System.Text.Json.Nodes;
using SkeletonKey.Runner.Core;
using SkeletonKey.Runtime;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>
/// Covers runner command envelopes without launching a separate process.
/// </summary>
public sealed class SkeletonKeyRunnerTests
{
    /// <summary>
    /// Verifies version returns a successful net10.0 envelope.
    /// </summary>
    [Fact]
    public async Task VersionReturnsSuccessfulEnvelope()
    {
        StringWriter output = new();
        int exitCode = await new SkeletonKeyRunner(TextReader.Null, output, TextWriter.Null).ExecuteAsync(["version"]);

        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.Equal(RunnerExitCodes.Success, exitCode);
        Assert.True(envelope["accepted"]!.GetValue<bool>());
        Assert.Equal("net10.0", envelope["result"]!["targetFramework"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies validate reads workflow JSON from stdin.
    /// </summary>
    [Fact]
    public async Task ValidateReadsWorkflowFromStandardInput()
    {
        string workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "fixtures", "conformance", "valid", "core-return.workflow.json"));
        StringWriter output = new();
        int exitCode = await new SkeletonKeyRunner(new StringReader(workflow), output, TextWriter.Null).ExecuteAsync(["validate", "--file", "-"]);

        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.Equal(RunnerExitCodes.Success, exitCode);
        Assert.True(envelope["accepted"]!.GetValue<bool>());
        Assert.Equal("valid", envelope["status"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies plan reports catalog analysis failures as a structured blocked envelope.
    /// </summary>
    [Fact]
    public async Task PlanReturnsBlockedEnvelopeWhenAnalysisFails()
    {
        string workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), "examples", "minimal.workflow.json"));
        StringWriter output = new();
        int exitCode = await new SkeletonKeyRunner(new StringReader(workflow), output, TextWriter.Null).ExecuteAsync(["plan", "--file", "-"]);

        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.Equal(RunnerExitCodes.Failed, exitCode);
        Assert.False(envelope["accepted"]!.GetValue<bool>());
        Assert.Equal("blocked", envelope["status"]!.GetValue<string>());
        Assert.Equal("SKA1001", envelope["issues"]![0]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies NDJSON mode emits a typed result record for non-runtime commands.
    /// </summary>
    [Fact]
    public async Task VersionSupportsNdjsonOutput()
    {
        StringWriter output = new();
        int exitCode = await new SkeletonKeyRunner(TextReader.Null, output, TextWriter.Null).ExecuteAsync(["version", "--format", "ndjson"]);

        string[] lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        JsonNode record = JsonNode.Parse(Assert.Single(lines))!;
        Assert.Equal(RunnerExitCodes.Success, exitCode);
        Assert.Equal("result", record["type"]!.GetValue<string>());
        Assert.True(record["envelope"]!["accepted"]!.GetValue<bool>());
    }

    /// <summary>
    /// Verifies diagnostics are opt-in, bounded, and separated from machine output.
    /// </summary>
    [Fact]
    public async Task DiagnosticsAreWrittenToStandardErrorOnlyWhenRequested()
    {
        StringWriter output = new();
        StringWriter error = new();
        int exitCode = await new SkeletonKeyRunner(TextReader.Null, output, error).ExecuteAsync(["version", "--diagnostics"]);

        Assert.Equal(RunnerExitCodes.Success, exitCode);
        Assert.Contains("[command-start] version", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("[command-complete] succeeded", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command-start", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies machine output redacts values carried by conventionally sensitive keys.
    /// </summary>
    [Fact]
    public async Task RunnerDoesNotEchoInputSecretsInFailureOutput()
    {
        StringWriter output = new();
        string invalidWorkflow = "{\"password\":\"runner-secret-value\"}";
        await new SkeletonKeyRunner(new StringReader(invalidWorkflow), output, TextWriter.Null).ExecuteAsync(["validate", "--file", "-"]);

        Assert.DoesNotContain("runner-secret-value", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Verifies run persists a terminal checkpoint and resume returns it successfully.</summary>
    [Fact]
    public async Task RunAndResumeUseDurableCheckpointDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "skeletonkey-runner-checkpoints", Guid.NewGuid().ToString("N"));
        string workflow = Path.Combine(RepositoryRoot(), "tests", "fixtures", "conformance", "valid", "core-return.workflow.json");
        try
        {
            StringWriter runOutput = new();
            int runExitCode = await new SkeletonKeyRunner(TextReader.Null, runOutput, TextWriter.Null).ExecuteAsync([
                "run", "--file", workflow, "--execution-id", "runner-checkpoint", "--checkpoint-directory", root,
            ]);
            StringWriter resumeOutput = new();
            int resumeExitCode = await new SkeletonKeyRunner(TextReader.Null, resumeOutput, TextWriter.Null).ExecuteAsync([
                "resume", "--file", workflow, "--execution-id", "runner-checkpoint", "--checkpoint-directory", root,
            ]);

            JsonNode runEnvelope = JsonNode.Parse(runOutput.ToString())!;
            JsonNode resumeEnvelope = JsonNode.Parse(resumeOutput.ToString())!;
            Assert.Equal(RunnerExitCodes.Success, runExitCode);
            Assert.Equal(RunnerExitCodes.Success, resumeExitCode);
            Assert.Equal("run", runEnvelope["command"]!.GetValue<string>());
            Assert.Equal("resume", resumeEnvelope["command"]!.GetValue<string>());
            Assert.Equal(runEnvelope["result"]!["executionId"]!.GetValue<string>(), resumeEnvelope["result"]!["executionId"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>Verifies resume reports a stable failure when no checkpoint exists.</summary>
    [Fact]
    public async Task ResumeRejectsMissingCheckpoint()
    {
        string root = Path.Combine(Path.GetTempPath(), "skeletonkey-runner-checkpoints", Guid.NewGuid().ToString("N"));
        string workflow = Path.Combine(RepositoryRoot(), "tests", "fixtures", "conformance", "valid", "core-return.workflow.json");
        try
        {
            StringWriter output = new();
            int exitCode = await new SkeletonKeyRunner(TextReader.Null, output, TextWriter.Null).ExecuteAsync([
                "resume", "--file", workflow, "--execution-id", "missing", "--checkpoint-directory", root,
            ]);

            JsonNode envelope = JsonNode.Parse(output.ToString())!;
            Assert.Equal(RunnerExitCodes.Failed, exitCode);
            Assert.Equal(WorkflowCheckpointErrorCodes.InvalidCheckpoint, envelope["issues"]![0]!["code"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>Verifies analyze reports a structured missing child workflow dependency.</summary>
    [Fact]
    public async Task AnalyzeRejectsMissingWorkflowDependency()
    {
        string workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "fixtures", "conformance", "valid", "invoke-workflow-forward-streams.workflow.json"));
        StringWriter output = new();

        int exitCode = await new SkeletonKeyRunner(new StringReader(workflow), output, TextWriter.Null).ExecuteAsync(["analyze", "--file", "-"]);

        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.Equal(RunnerExitCodes.Failed, exitCode);
        Assert.Equal("blocked", envelope["status"]!.GetValue<string>());
        Assert.Equal("SKD1001", envelope["issues"]![0]!["code"]!.GetValue<string>());
    }

    /// <summary>Verifies run loads an exact versioned child workflow from the explicit workflow directory.</summary>
    [Fact]
    public async Task RunLoadsVersionedWorkflowDependencyDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "skeletonkey-runner-workflows", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string rootPath = Path.Combine(RepositoryRoot(), "tests", "fixtures", "conformance", "valid", "invoke-workflow-forward-streams.workflow.json");
            string child = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "fixtures", "conformance", "valid", "core-return.workflow.json"))
                .Replace("\"id\": \"core-return\"", "\"id\": \"child-workflow\"", StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(directory, "child-workflow@1.0.0.workflow.json"), child);
            StringWriter output = new();

            int exitCode = await new SkeletonKeyRunner(TextReader.Null, output, TextWriter.Null).ExecuteAsync([
                "run", "--file", rootPath, "--workflow-directory", directory,
            ]);

            JsonNode envelope = JsonNode.Parse(output.ToString())!;
            Assert.Equal(RunnerExitCodes.Success, exitCode);
            Assert.True(envelope["accepted"]!.GetValue<bool>());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo current = new(AppContext.BaseDirectory);
        while (current.Parent is not null && !File.Exists(Path.Combine(current.FullName, "SkeletonKey.sln")))
        {
            current = current.Parent;
        }

        return current.FullName;
    }
}
