using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Materialization;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Invocation;
using SkeletonKey.Secrets.Abstractions;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Runtime.Default.Tests;

/// <summary>Covers Phase 4 secret resolution and runtime-owned time semantics.</summary>
public sealed class Phase4RuntimeTests
{
    /// <summary>Verifies a secret is revealed only to the target handler and not retained in framework events or checkpoints.</summary>
    [Fact]
    public async Task ResolvesSecretJustInTimeWithoutCheckpointOrEventLeak()
    {
        const string secret = "phase4-super-secret-value";
        CaptureHandler handler = new();
        RecordingCheckpointStore checkpoints = new();
        RecordingSink events = new();
        DefaultWorkflowRuntime runtime = Runtime(handler, new FixedSecretProvider("api-key", secret));
        WorkflowDocument workflow = CaptureWorkflow();

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(
            workflow,
            "phase4-secret",
            "phase4-secret-plan",
            eventSink: events,
            checkpointStore: checkpoints));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal(secret, handler.LastValue);
        string checkpointJson = JsonSerializer.Serialize(checkpoints.Current);
        string eventJson = JsonSerializer.Serialize(events.Events);
        Assert.DoesNotContain(secret, checkpointJson, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, eventJson, StringComparison.Ordinal);
    }

    /// <summary>Verifies missing secret-provider configuration fails with a stable runtime code.</summary>
    [Fact]
    public async Task SecretReferenceRequiresProvider()
    {
        CaptureHandler handler = new();

        WorkflowRuntimeResult result = await Runtime(handler, secretProvider: null).ExecuteAsync(
            new WorkflowExecutionRequest(CaptureWorkflow(), "phase4-no-provider", "phase4-plan"));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.RuntimeSecretProviderUnavailable, result.Result.Error!.Code);
        Assert.Null(handler.LastValue);
    }

    /// <summary>Verifies an unresolved name fails without including the name or a secret value in the technical error.</summary>
    [Fact]
    public async Task UnknownSecretFailsClosed()
    {
        CaptureHandler handler = new();

        WorkflowRuntimeResult result = await Runtime(handler, new FixedSecretProvider("other", "value")).ExecuteAsync(
            new WorkflowExecutionRequest(CaptureWorkflow(), "phase4-missing-secret", "phase4-plan"));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.RuntimeSecretNotFound, result.Result.Error!.Code);
        Assert.DoesNotContain("api-key", result.Result.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a child workflow receives the same host secret boundary.</summary>
    [Fact]
    public async Task SecretProviderPropagatesIntoInvokedWorkflow()
    {
        CaptureHandler handler = new();
        WorkflowDocument child = CaptureWorkflow("child");
        WorkflowDocument parent = new(
            id: "parent",
            name: "Parent",
            nodes:
            [
                new("start", "core.start", 1),
                new("invoke", "workflow.invoke", 1, parameters: new JsonObject
                {
                    ["workflow"] = new JsonObject { ["id"] = "child" },
                }),
                Return("done"),
            ],
            connections:
            [
                Connect("start", "main", "invoke", "main"),
                Connect("invoke", "result", "done", "main"),
            ]);

        DefaultWorkflowRuntime runtime = Runtime(
            handler,
            new FixedSecretProvider("api-key", "child-secret"),
            ImmutableWorkflowRepository.FromDocuments(child));

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(parent, "phase4-child", "phase4-child-plan"));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal("child-secret", handler.LastValue);
    }

    /// <summary>Verifies time.now uses the supplied runtime clock rather than the system clock.</summary>
    [Fact]
    public async Task TimeNowUsesRuntimeClock()
    {
        DateTimeOffset expected = new(2026, 9, 18, 12, 34, 56, 789, TimeSpan.Zero);
        WorkflowDocument workflow = new(
            id: "clock-workflow",
            name: "Clock Workflow",
            nodes:
            [
                new("start", "core.start", 1),
                new("clock", "time.now", 1),
                Return("done"),
            ],
            connections:
            [
                Connect("start", "main", "clock", "main"),
                Connect("clock", "continue", "done", "main"),
            ]);
        DefaultWorkflowRuntime runtime = new(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            BuiltInWorkflowNodeCatalog.Catalog,
            BuiltInRuntimeHandlers.CreateResolver(),
            new NodeParameterMaterializer(),
            new FixedClock(expected));

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(workflow, "phase4-clock", "phase4-clock-plan"));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        NodeExecutionResult node = result.NodeResults.Single(static item => item.NodeId == "clock");
        Assert.Equal(expected.ToString("O", System.Globalization.CultureInfo.InvariantCulture), node.Outputs["utc"]!.GetValue<string>());
        Assert.Equal(expected.ToUnixTimeMilliseconds(), node.Outputs["unixTimeMilliseconds"]!.GetValue<long>());
    }

    private static DefaultWorkflowRuntime Runtime(
        CaptureHandler handler,
        IWorkflowSecretProvider? secretProvider,
        IWorkflowRepository? repository = null)
    {
        WorkflowNodeDefinition capture = new(
            "demo.capture",
            1,
            parametersSchema: new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("value"),
            },
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Ports(WorkflowPortDirection.Output, "next"));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, capture]);
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver([.. BuiltInRuntimeHandlers.Create(), handler]),
            new NodeParameterMaterializer(),
            workflowRepository: repository,
            secretProvider: secretProvider);
    }

    private static WorkflowDocument CaptureWorkflow(string id = "secret-workflow")
    {
        return new WorkflowDocument(
            id: id,
            name: "Secret Workflow",
            nodes:
            [
                new("start", "core.start", 1),
                new("capture", "demo.capture", 1, parameters: new JsonObject
                {
                    ["value"] = new JsonObject { ["$secret"] = "api-key" },
                }),
                Return("done"),
            ],
            connections:
            [
                Connect("start", "main", "capture", "main"),
                Connect("capture", "next", "done", "main"),
            ]);
    }

    private static WorkflowNode Return(string id)
    {
        return new(id, "core.return", 1, parameters: new JsonObject
        {
            ["outcome"] = new JsonObject
            {
                ["kind"] = "success",
                ["code"] = "done",
            },
        });
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(WorkflowPortDirection direction, string name)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, direction),
        };
    }

    private sealed class CaptureHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.capture", 1);

        public string? LastValue { get; private set; }

        public ValueTask<NodeHandlerResult> ExecuteAsync(
            NodeExecutionRequest request,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastValue = request.Parameters["value"]!.GetValue<string>();
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["next"])));
        }
    }

    private sealed class FixedSecretProvider(string availableName, string value) : IWorkflowSecretProvider
    {
        public ValueTask<WorkflowSecretValue?> ResolveAsync(string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(string.Equals(name, availableName, StringComparison.Ordinal)
                ? new WorkflowSecretValue(value)
                : null);
        }
    }

    private sealed class FixedClock(DateTimeOffset value) : IWorkflowClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed class RecordingSink : IWorkflowEventSink
    {
        private readonly List<WorkflowEvent> _events = [];

        public IReadOnlyList<WorkflowEvent> Events => _events.AsReadOnly();

        public ValueTask PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add(workflowEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCheckpointStore : IWorkflowCheckpointStore
    {
        public WorkflowExecutionCheckpoint? Current { get; private set; }

        public ValueTask<WorkflowExecutionCheckpoint?> LoadAsync(string executionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Current);
        }

        public ValueTask SaveAsync(
            WorkflowExecutionCheckpoint checkpoint,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = checkpoint;
            return ValueTask.CompletedTask;
        }
    }
}
