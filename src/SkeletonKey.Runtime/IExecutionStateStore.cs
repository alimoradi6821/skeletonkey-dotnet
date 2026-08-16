using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;

namespace SkeletonKey.Runtime;

/// <summary>
/// Defines in-process execution state storage for immutable runtime snapshots and validated lifecycle transitions.
/// </summary>
/// <remarks>
/// The state store is responsible for snapshots, revisions, timestamps supplied by the runtime, thread safety, and legal transition enforcement.
/// It does not provide persistence, checkpointing, resume, distributed execution, or host service location.
/// </remarks>
public interface IExecutionStateStore
{
    /// <summary>Creates the root execution snapshot in the Created state.</summary>
    public WorkflowExecutionStateSnapshot CreateExecution(string executionId, string rootWorkflowId, string planId, DateTimeOffset timestamp);

    /// <summary>Creates a workflow invocation snapshot in the Created state.</summary>
    public WorkflowInvocationStateSnapshot CreateInvocation(string executionId, string invocationId, string? parentInvocationId, string workflowId, DateTimeOffset timestamp);

    /// <summary>Creates a node execution snapshot in the Created state.</summary>
    public NodeExecutionStateSnapshot CreateNode(NodeExecutionIdentity identity, string nodeExecutionId, DateTimeOffset timestamp);

    /// <summary>Applies a validated root execution lifecycle transition and returns the new immutable snapshot.</summary>
    public WorkflowExecutionStateSnapshot TransitionExecution(string executionId, ExecutionLifecycleState next, DateTimeOffset timestamp, WorkflowExecutionResult? result = null);

    /// <summary>Applies a validated invocation lifecycle transition and returns the new immutable snapshot.</summary>
    public WorkflowInvocationStateSnapshot TransitionInvocation(string invocationId, ExecutionLifecycleState next, DateTimeOffset timestamp, WorkflowExecutionResult? result = null);

    /// <summary>Applies a validated node lifecycle transition and returns the new immutable snapshot.</summary>
    public NodeExecutionStateSnapshot TransitionNode(string nodeExecutionId, ExecutionLifecycleState next, DateTimeOffset timestamp, NodeExecutionResult? result = null);

    /// <summary>Gets the latest root execution snapshot.</summary>
    public WorkflowExecutionStateSnapshot GetExecution(string executionId);

    /// <summary>Gets the latest invocation snapshot.</summary>
    public WorkflowInvocationStateSnapshot GetInvocation(string invocationId);

    /// <summary>Gets the latest node execution snapshot.</summary>
    public NodeExecutionStateSnapshot GetNode(string nodeExecutionId);
}
