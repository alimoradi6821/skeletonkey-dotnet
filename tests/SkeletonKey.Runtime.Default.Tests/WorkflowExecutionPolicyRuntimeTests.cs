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
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Policies;

namespace SkeletonKey.Runtime.Default.Tests;

/// <summary>Covers runtime-owned timeout, retry, backoff, and on-error execution policy behavior.</summary>
public sealed class WorkflowExecutionPolicyRuntimeTests
{
    /// <summary>Verifies expected failures retry with distinct identities and bounded exponential delays.</summary>
    [Fact]
    public async Task RetriesUntilSuccessWithDistinctAttemptIdentities()
    {
        RetryThenSucceedHandler handler = new(failuresBeforeSuccess: 2);
        RecordingDelay delay = new();
        RecordingSink sink = new();
        WorkflowExecutionPolicy policy = new(retry: new WorkflowRetryPolicy(maxAttempts: 3, delay: "PT1S", backoff: 2, maxDelay: "PT1.5S"));

        WorkflowRuntimeResult result = await Runtime(handler, delay).ExecuteAsync(Request(Workflow(policy), sink));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal([1, 2, 3], handler.Attempts);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.5)], delay.Delays);
        Assert.Equal(2, result.NodeResults.Count(static item => item.NodeId == "policy" && item.Status == NodeExecutionStatus.Failed));
        Assert.Contains(result.NodeResults, static item => item.NodeId == "policy" && item.Status == NodeExecutionStatus.Succeeded && item.Attempt == 3);
        Assert.Equal(2, sink.Events.Count(static item => item.Kind == RuntimeWorkflowEventKind.NodeRetryScheduled));
        Assert.Equal(2, sink.Events.Count(static item => item.Kind == RuntimeWorkflowEventKind.NodeRetryStarted));
    }

    /// <summary>Verifies a timeout cancels each attempt and exhausts the declared maximum attempt count.</summary>
    [Fact]
    public async Task TimeoutRetriesThenFailsWithStableErrorCode()
    {
        TimeoutHandler handler = new();
        WorkflowExecutionPolicy policy = new(timeout: "PT0.01S", retry: new WorkflowRetryPolicy(maxAttempts: 2));

        WorkflowRuntimeResult result = await Runtime(handler, new RecordingDelay()).ExecuteAsync(Request(Workflow(policy)));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.NodeExecutionTimedOut, result.Result.Error!.Code);
        Assert.Equal(2, handler.ExecutionCount);
        Assert.Equal(2, result.NodeResults.Count(static item => item.NodeId == "policy" && item.Status == NodeExecutionStatus.Failed));
    }

    /// <summary>Verifies continue consumes the terminal error and activates the conventional next control output.</summary>
    [Fact]
    public async Task ContinuePolicyActivatesNextAndCompletesWorkflow()
    {
        AlwaysFailHandler handler = new();
        RecordingSink sink = new();
        WorkflowExecutionPolicy policy = new(onError: WorkflowOnError.Continue);

        WorkflowRuntimeResult result = await Runtime(handler, new RecordingDelay()).ExecuteAsync(Request(Workflow(policy), sink));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Null(result.Result.Error);
        Assert.Contains(result.NodeResults, static item => item.NodeId == "policy" && item.Status == NodeExecutionStatus.Failed);
        Assert.Contains(result.NodeResults, static item => item.NodeId == "done" && item.Status == NodeExecutionStatus.Succeeded);
        Assert.Contains(sink.Events, static item => item.Kind == RuntimeWorkflowEventKind.NodeErrorContinued && item.NodeId == "policy");
    }

    /// <summary>Verifies stop converts the original node failure to a stable workflow stop result.</summary>
    [Fact]
    public async Task StopPolicyTerminatesWithoutExecutingNextNode()
    {
        AlwaysFailHandler handler = new();
        RecordingSink sink = new();
        WorkflowExecutionPolicy policy = new(onError: WorkflowOnError.Stop);

        WorkflowRuntimeResult result = await Runtime(handler, new RecordingDelay()).ExecuteAsync(Request(Workflow(policy), sink));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.NodeExecutionStopped, result.Result.Error!.Code);
        Assert.DoesNotContain(result.NodeResults, static item => item.NodeId == "done" && item.Status == NodeExecutionStatus.Succeeded);
        Assert.Contains(sink.Events, static item => item.Kind == RuntimeWorkflowEventKind.NodeExecutionStopped && item.NodeId == "policy");
    }

    /// <summary>Verifies repeated loop-body handler activations use the same timeout and retry policy engine.</summary>
    [Fact]
    public async Task LoopBodyHandlerRetriesInsideItsActivation()
    {
        RetryThenSucceedHandler handler = new(failuresBeforeSuccess: 1);
        RecordingDelay delay = new();
        WorkflowExecutionPolicy policy = new(retry: new WorkflowRetryPolicy(maxAttempts: 2, delay: "PT0.25S"));

        WorkflowRuntimeResult result = await Runtime(handler, delay).ExecuteAsync(Request(LoopWorkflow(policy)));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal([1, 2], handler.Attempts);
        Assert.Equal([TimeSpan.FromMilliseconds(250)], delay.Delays);
        Assert.Contains(result.NodeResults, static item => item.NodeId == "policy" && item.Status == NodeExecutionStatus.Failed && item.Attempt == 1);
        Assert.Contains(result.NodeResults, static item => item.NodeId == "policy" && item.Status == NodeExecutionStatus.Succeeded && item.Attempt == 2);
    }

    /// <summary>Verifies resume begins the next retry instead of repeating the failed attempt at the persisted safe boundary.</summary>
    [Fact]
    public async Task ResumeRetryBoundaryDoesNotRepeatFailedAttempt()
    {
        RetryThenSucceedHandler handler = new(failuresBeforeSuccess: 1);
        RecordingDelay delay = new();
        WorkflowExecutionPolicy policy = new(retry: new WorkflowRetryPolicy(maxAttempts: 2));
        WorkflowDocument workflow = Workflow(policy);
        FailingCheckpointStore firstStore = new(failFromSave: 6);
        DefaultWorkflowRuntime runtime = Runtime(handler, delay);

        WorkflowRuntimeResult interrupted = await runtime.ExecuteAsync(Request(workflow, checkpointStore: firstStore));

        Assert.Equal(WorkflowExecutionStatus.Failed, interrupted.Result.Status);
        Assert.Equal([1], handler.Attempts);
        WorkflowExecutionCheckpoint safe = Assert.IsType<WorkflowExecutionCheckpoint>(firstStore.Current);
        WorkflowCheckpointStep retryStep = Assert.Single(safe.Steps, static item => item.NodeId == "policy");
        Assert.Equal(WorkflowStepRuntimeStatus.Ready, retryStep.Status);
        Assert.Equal(1, retryStep.RetryAttempt);
        Assert.NotNull(retryStep.RetryNotBeforeUtc);

        FailingCheckpointStore resumedStore = new(safe);
        WorkflowRuntimeResult resumed = await runtime.ExecuteAsync(Request(workflow, checkpointStore: resumedStore, resumeCheckpoint: safe));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, resumed.Result.Status);
        Assert.Equal([1, 2], handler.Attempts);
        Assert.True(resumedStore.Current!.IsTerminal);
    }

    private static DefaultWorkflowRuntime Runtime(INodeHandler handler, IWorkflowRuntimeDelay delay)
    {
        WorkflowNodeDefinition definition = new(
            "demo.policy",
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Ports(WorkflowPortDirection.Output, "next"));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, definition]);
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver([.. BuiltInRuntimeHandlers.Create(), handler]),
            new NodeParameterMaterializer(),
            new FixedClock(),
            new WorkflowRuntimeOptions(),
            delay: delay);
    }

    private static WorkflowExecutionRequest Request(
        WorkflowDocument workflow,
        IWorkflowEventSink? sink = null,
        IWorkflowCheckpointStore? checkpointStore = null,
        WorkflowExecutionCheckpoint? resumeCheckpoint = null)
    {
        return new WorkflowExecutionRequest(
            workflow,
            "policy-execution",
            "policy-plan",
            eventSink: sink,
            checkpointStore: checkpointStore,
            resumeCheckpoint: resumeCheckpoint);
    }

    private static WorkflowDocument Workflow(WorkflowExecutionPolicy policy)
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            new("policy", "demo.policy", 1, policy: policy),
            new(
                "done",
                "core.return",
                1,
                parameters: new JsonObject
                {
                    ["outcome"] = new JsonObject
                    {
                        ["kind"] = "success",
                        ["code"] = "done",
                    },
                }),
        ];
        WorkflowConnection[] connections =
        [
            Connect("start", "main", "policy", "main"),
            Connect("policy", "next", "done", "main"),
        ];
        return new WorkflowDocument(id: "policy-workflow", name: "Policy Workflow", nodes: nodes, connections: connections);
    }

    private static WorkflowDocument LoopWorkflow(WorkflowExecutionPolicy policy)
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            new("loop", "flow.repeat", 1, parameters: new JsonObject { ["count"] = 1 }),
            new("policy", "demo.policy", 1, policy: policy),
            new(
                "done",
                "core.return",
                1,
                parameters: new JsonObject
                {
                    ["outcome"] = new JsonObject
                    {
                        ["kind"] = "success",
                        ["code"] = "done",
                    },
                }),
        ];
        WorkflowConnection[] connections =
        [
            Connect("start", "main", "loop", "main"),
            Connect("loop", "body", "policy", "main"),
            Connect("policy", "next", "loop", "continue"),
            Connect("loop", "completed", "done", "main"),
        ];
        return new WorkflowDocument(id: "loop-policy-workflow", name: "Loop Policy Workflow", nodes: nodes, connections: connections);
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(WorkflowPortDirection direction, string name)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, direction),
        };
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private sealed class RetryThenSucceedHandler(int failuresBeforeSuccess) : INodeHandler
    {
        private readonly List<int> _attempts = [];

        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.policy", 1);

        public IReadOnlyList<int> Attempts => _attempts.ToArray();

        public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _attempts.Add(request.Identity.Attempt);
            NodeHandlerResult result = _attempts.Count <= failuresBeforeSuccess
                ? NodeHandlerResult.Failure(new WorkflowError("TEST-POLICY", "Expected retryable test failure.", request.Identity.NodeId))
                : NodeHandlerResult.Success(new NodeHandlerOutputs(["next"]));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AlwaysFailHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.policy", 1);

        public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NodeHandlerResult.Failure(new WorkflowError("TEST-POLICY", "Expected policy failure.", request.Identity.NodeId)));
        }
    }

    private sealed class TimeoutHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.policy", 1);

        public int ExecutionCount { get; private set; }

        public async ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return NodeHandlerResult.Success();
        }
    }

    private sealed class RecordingDelay : IWorkflowRuntimeDelay
    {
        private readonly List<TimeSpan> _delays = [];

        public IReadOnlyList<TimeSpan> Delays => _delays.ToArray();

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _delays.Add(delay);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedClock : IWorkflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class RecordingSink : IWorkflowEventSink
    {
        private readonly List<RuntimeWorkflowEvent> _events = [];

        public IReadOnlyList<RuntimeWorkflowEvent> Events => _events.ToArray();

        public ValueTask PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workflowEvent is RuntimeWorkflowEvent runtimeEvent)
            {
                _events.Add(runtimeEvent);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingCheckpointStore : IWorkflowCheckpointStore
    {
        private readonly int? _failFromSave;
        private int _saves;

        public FailingCheckpointStore(int failFromSave)
        {
            _failFromSave = failFromSave;
        }

        public FailingCheckpointStore(WorkflowExecutionCheckpoint initial)
        {
            Current = initial;
        }

        public WorkflowExecutionCheckpoint? Current { get; private set; }

        public ValueTask<WorkflowExecutionCheckpoint?> LoadAsync(string executionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Current is not null && string.Equals(Current.ExecutionId, executionId, StringComparison.Ordinal) ? Current : null);
        }

        public ValueTask SaveAsync(WorkflowExecutionCheckpoint checkpoint, long expectedRevision, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _saves++;
            if (_failFromSave is not null && _saves >= _failFromSave.Value)
            {
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "Simulated process stop.");
            }

            Assert.Equal(Current?.Revision ?? 0, expectedRevision);
            Assert.Equal(expectedRevision + 1, checkpoint.Revision);
            Current = checkpoint;
            return ValueTask.CompletedTask;
        }
    }
}
