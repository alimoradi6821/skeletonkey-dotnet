using System.Text.Json.Nodes;
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

namespace SkeletonKey.Runtime.Default.Tests;

/// <summary>Covers durable safe-boundary checkpoint and resume behavior.</summary>
public sealed class WorkflowCheckpointRuntimeTests
{
    /// <summary>Verifies a completed custom node is not invoked again after process-style resume.</summary>
    [Fact]
    public async Task ResumeDoesNotExecuteCompletedNodeTwice()
    {
        CountingHandler handler = new();
        WorkflowDocument workflow = Workflow();
        FailingCheckpointStore firstStore = new(failFromSave: 6);
        DefaultWorkflowRuntime runtime = Runtime(handler);

        WorkflowRuntimeResult interrupted = await runtime.ExecuteAsync(Request(workflow, firstStore));

        Assert.Equal(WorkflowExecutionStatus.Failed, interrupted.Result.Status);
        Assert.Equal(1, handler.ExecutionCount);
        WorkflowExecutionCheckpoint safeCheckpoint = Assert.IsType<WorkflowExecutionCheckpoint>(firstStore.Current);
        Assert.Equal(WorkflowStepRuntimeStatus.Succeeded, safeCheckpoint.Steps.Single(static step => step.NodeId == "count").Status);

        FailingCheckpointStore resumedStore = new(safeCheckpoint);
        WorkflowRuntimeResult resumed = await runtime.ExecuteAsync(Request(workflow, resumedStore, safeCheckpoint));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, resumed.Result.Status);
        Assert.Equal(1, handler.ExecutionCount);
        Assert.True(resumedStore.Current!.IsTerminal);
    }

    /// <summary>Verifies a checkpoint captured while a node was running requires explicit recovery.</summary>
    [Fact]
    public async Task ResumeRejectsInterruptedRunningNode()
    {
        CountingHandler handler = new();
        WorkflowDocument workflow = Workflow();
        FailingCheckpointStore firstStore = new(failFromSave: 5);
        DefaultWorkflowRuntime runtime = Runtime(handler);
        await runtime.ExecuteAsync(Request(workflow, firstStore));
        WorkflowExecutionCheckpoint interruptedCheckpoint = Assert.IsType<WorkflowExecutionCheckpoint>(firstStore.Current);

        WorkflowRuntimeResult resumed = await runtime.ExecuteAsync(Request(workflow, new FailingCheckpointStore(interruptedCheckpoint), interruptedCheckpoint));

        Assert.Equal(WorkflowExecutionStatus.Failed, resumed.Result.Status);
        Assert.Equal(WorkflowCheckpointErrorCodes.InterruptedStepRequiresRecovery, resumed.Result.Error!.Code);
        Assert.Equal("count", resumed.Result.Error.NodeId);
    }

    /// <summary>Verifies terminal checkpoint resume returns the original result without invoking handlers.</summary>
    [Fact]
    public async Task ResumeTerminalCheckpointReturnsOriginalResult()
    {
        CountingHandler handler = new();
        WorkflowDocument workflow = Workflow();
        FailingCheckpointStore store = new();
        DefaultWorkflowRuntime runtime = Runtime(handler);
        WorkflowRuntimeResult first = await runtime.ExecuteAsync(Request(workflow, store));
        WorkflowExecutionCheckpoint terminal = Assert.IsType<WorkflowExecutionCheckpoint>(store.Current);

        WorkflowRuntimeResult resumed = await runtime.ExecuteAsync(Request(workflow, store, terminal));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, first.Result.Status);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, resumed.Result.Status);
        Assert.Equal(first.Result.Outputs, resumed.Result.Outputs);
        Assert.Equal(1, handler.ExecutionCount);
        Assert.Equal(3, resumed.NodeResults.Count);
        Assert.Equal(first.NodeSnapshots.Count, resumed.NodeSnapshots.Count);
    }

    /// <summary>Verifies changed execution inputs cannot be applied to an existing checkpoint.</summary>
    [Fact]
    public async Task ResumeRejectsChangedInputs()
    {
        CountingHandler handler = new();
        WorkflowDocument workflow = Workflow();
        FailingCheckpointStore store = new();
        DefaultWorkflowRuntime runtime = Runtime(handler);
        await runtime.ExecuteAsync(Request(workflow, store));
        WorkflowExecutionCheckpoint terminal = Assert.IsType<WorkflowExecutionCheckpoint>(store.Current);
        WorkflowExecutionRequest changed = new(
            workflow,
            "checkpoint-execution",
            "checkpoint-plan",
            inputs: new Dictionary<string, JsonNode?> { ["changed"] = JsonValue.Create(true) },
            checkpointStore: store,
            resumeCheckpoint: terminal);

        WorkflowRuntimeResult resumed = await runtime.ExecuteAsync(changed);

        Assert.Equal(WorkflowExecutionStatus.Failed, resumed.Result.Status);
        Assert.Equal(WorkflowCheckpointErrorCodes.IdentityMismatch, resumed.Result.Error!.Code);
        Assert.Equal(1, handler.ExecutionCount);
    }

    private static WorkflowExecutionRequest Request(WorkflowDocument workflow, IWorkflowCheckpointStore store, WorkflowExecutionCheckpoint? checkpoint = null)
    {
        return new WorkflowExecutionRequest(workflow, "checkpoint-execution", "checkpoint-plan", checkpointStore: store, resumeCheckpoint: checkpoint);
    }

    private static DefaultWorkflowRuntime Runtime(CountingHandler handler)
    {
        WorkflowNodeDefinition definition = new(
            "demo.count",
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
            new NodeParameterMaterializer());
    }

    private static WorkflowDocument Workflow()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            new("count", "demo.count", 1),
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
            Connect("start", "main", "count", "main"),
            Connect("count", "next", "done", "main"),
        ];
        return new WorkflowDocument(id: "checkpoint-workflow", name: "Checkpoint Workflow", nodes: nodes, connections: connections);
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

    private sealed class CountingHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.count", 1);

        public int ExecutionCount { get; private set; }

        public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["next"])));
        }
    }

    private sealed class FailingCheckpointStore : IWorkflowCheckpointStore
    {
        private readonly int? _failFromSave;
        private int _saves;

        public FailingCheckpointStore(int? failFromSave = null)
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
