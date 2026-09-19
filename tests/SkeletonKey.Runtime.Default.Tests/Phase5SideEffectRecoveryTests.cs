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

/// <summary>Covers fail-closed recovery for interrupted external side effects.</summary>
public sealed class Phase5SideEffectRecoveryTests
{
    /// <summary>A checkpoint proving dispatch never began may replay the handler exactly once.</summary>
    [Fact]
    public async Task NotDispatchedCheckpointReplaysExactlyOnce()
    {
        SideEffectHandler handler = new();
        CrashCheckpointStore crashStore = new(WorkflowSideEffectCheckpointState.NotDispatched);
        DefaultWorkflowRuntime firstRuntime = Runtime(handler);

        WorkflowRuntimeResult interrupted = await firstRuntime.ExecuteAsync(Request(crashStore));

        Assert.Equal(WorkflowExecutionStatus.Failed, interrupted.Result.Status);
        WorkflowExecutionCheckpoint safe = Assert.IsType<WorkflowExecutionCheckpoint>(crashStore.Current);
        WorkflowCheckpointStep interruptedStep = safe.Steps.Single(static step => step.NodeId == "effect");
        Assert.Equal(WorkflowStepRuntimeStatus.Running, interruptedStep.Status);
        Assert.Equal(WorkflowSideEffectCheckpointState.NotDispatched, interruptedStep.SideEffectState);
        Assert.Equal(0, handler.ExecutionCount);

        MemoryCheckpointStore resumedStore = new(safe);
        WorkflowRuntimeResult resumed = await Runtime(handler).ExecuteAsync(Request(resumedStore, safe));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, resumed.Result.Status);
        Assert.Equal(1, handler.ExecutionCount);
    }

    /// <summary>An uncertain dispatch without a reconciler is surfaced and never blindly replayed.</summary>
    [Fact]
    public async Task DispatchUncertainWithoutReconcilerNeverReplays()
    {
        SideEffectHandler handler = new();
        CrashCheckpointStore crashStore = new(WorkflowSideEffectCheckpointState.DispatchUncertain);
        _ = await Runtime(handler).ExecuteAsync(Request(crashStore));
        WorkflowExecutionCheckpoint safe = Assert.IsType<WorkflowExecutionCheckpoint>(crashStore.Current);

        WorkflowRuntimeResult resumed = await Runtime(handler).ExecuteAsync(Request(new MemoryCheckpointStore(safe), safe));

        Assert.Equal(WorkflowExecutionStatus.Failed, resumed.Result.Status);
        Assert.Equal(WorkflowCheckpointErrorCodes.ExternalSideEffectOutcomeUncertain, resumed.Result.Error!.Code);
        Assert.Equal(0, handler.ExecutionCount);
    }

    /// <summary>A reconciler may confirm completion and supply outputs without invoking the external side effect again.</summary>
    [Fact]
    public async Task DispatchUncertainCanRecoverCompletedWithoutReplay()
    {
        RecoverableSideEffectHandler handler = new(NodeSideEffectRecoveryResult.Completed(new NodeHandlerOutputs(["next"])));
        CrashCheckpointStore crashStore = new(WorkflowSideEffectCheckpointState.DispatchUncertain);
        _ = await Runtime(handler).ExecuteAsync(Request(crashStore));
        WorkflowExecutionCheckpoint safe = Assert.IsType<WorkflowExecutionCheckpoint>(crashStore.Current);

        MemoryCheckpointStore resumedStore = new(safe);
        WorkflowRuntimeResult resumed = await Runtime(handler).ExecuteAsync(Request(resumedStore, safe));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, resumed.Result.Status);
        Assert.Equal(0, handler.ExecutionCount);
        Assert.Equal(1, handler.ReconciliationCount);
        WorkflowCheckpointStep recovered = resumedStore.Current!.Steps.Single(static step => step.NodeId == "effect");
        Assert.Equal(WorkflowStepRuntimeStatus.Succeeded, recovered.Status);
        Assert.Equal(WorkflowSideEffectCheckpointState.Completed, recovered.SideEffectState);
    }

    /// <summary>A reconciler may prove dispatch never occurred, permitting exactly one normal replay.</summary>
    [Fact]
    public async Task ReconciledNotAttemptedReplaysExactlyOnce()
    {
        RecoverableSideEffectHandler handler = new(NodeSideEffectRecoveryResult.NotAttempted());
        CrashCheckpointStore crashStore = new(WorkflowSideEffectCheckpointState.DispatchUncertain);
        _ = await Runtime(handler).ExecuteAsync(Request(crashStore));
        WorkflowExecutionCheckpoint safe = Assert.IsType<WorkflowExecutionCheckpoint>(crashStore.Current);

        WorkflowRuntimeResult resumed = await Runtime(handler).ExecuteAsync(Request(new MemoryCheckpointStore(safe), safe));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, resumed.Result.Status);
        Assert.Equal(1, handler.ExecutionCount);
        Assert.Equal(1, handler.ReconciliationCount);
    }

    private static DefaultWorkflowRuntime Runtime(INodeHandler handler)
    {
        WorkflowNodeDefinition effect = new(
            "demo.externalEffect",
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Ports(WorkflowPortDirection.Output, "next"));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, effect]);
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver([.. BuiltInRuntimeHandlers.Create(), handler]),
            new NodeParameterMaterializer());
    }

    private static WorkflowExecutionRequest Request(IWorkflowCheckpointStore store, WorkflowExecutionCheckpoint? checkpoint = null)
    {
        return new WorkflowExecutionRequest(
            Workflow(),
            "phase5-side-effect",
            "phase5-side-effect-plan",
            checkpointStore: store,
            resumeCheckpoint: checkpoint);
    }

    private static WorkflowDocument Workflow()
    {
        return new WorkflowDocument(
            id: "phase5-side-effect-workflow",
            name: "Phase 5 Side Effect Workflow",
            nodes:
            [
                new("start", "core.start", 1),
                new("effect", "demo.externalEffect", 1),
                new("done", "core.return", 1, parameters: new JsonObject
                {
                    ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" },
                }),
            ],
            connections:
            [
                Connect("start", "main", "effect", "main"),
                Connect("effect", "next", "done", "main"),
            ]);
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

    private sealed class SideEffectHandler : IExternalSideEffectNodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.externalEffect", 1);

        public int ExecutionCount { get; private set; }

        public ValueTask<NodeHandlerResult> ExecuteAsync(
            NodeExecutionRequest request,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["next"])));
        }
    }

    private sealed class RecoverableSideEffectHandler(NodeSideEffectRecoveryResult reconciliation) : INodeSideEffectRecoveryHandler
    {
        private readonly NodeSideEffectRecoveryResult _reconciliation = reconciliation;

        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.externalEffect", 1);

        public int ExecutionCount { get; private set; }

        public int ReconciliationCount { get; private set; }

        public ValueTask<NodeHandlerResult> ExecuteAsync(
            NodeExecutionRequest request,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["next"])));
        }

        public ValueTask<NodeSideEffectRecoveryResult> ReconcileAsync(
            NodeExecutionRequest request,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReconciliationCount++;
            return ValueTask.FromResult(_reconciliation);
        }
    }

    private sealed class CrashCheckpointStore(WorkflowSideEffectCheckpointState trigger) : IWorkflowCheckpointStore
    {
        private readonly WorkflowSideEffectCheckpointState _trigger = trigger;
        private bool _crashed;

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
            if (_crashed)
            {
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "Simulated process stop.");
            }

            Assert.Equal(Current?.Revision ?? 0, expectedRevision);
            Current = checkpoint;
            if (checkpoint.Steps.Any(step =>
                string.Equals(step.NodeId, "effect", StringComparison.Ordinal) &&
                step.Status == WorkflowStepRuntimeStatus.Running &&
                step.SideEffectState == _trigger))
            {
                _crashed = true;
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "Simulated process stop.");
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class MemoryCheckpointStore(WorkflowExecutionCheckpoint initial) : IWorkflowCheckpointStore
    {
        public WorkflowExecutionCheckpoint? Current { get; private set; } = initial;

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
            Assert.Equal(Current?.Revision ?? 0, expectedRevision);
            Current = checkpoint;
            return ValueTask.CompletedTask;
        }
    }
}
