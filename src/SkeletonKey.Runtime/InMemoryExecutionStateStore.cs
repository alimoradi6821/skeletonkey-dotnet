using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;

namespace SkeletonKey.Runtime;

/// <summary>
/// Provides a thread-safe in-memory execution-state store with deterministic revisions and legal transition enforcement.
/// </summary>
/// <remarks>
/// This store is per-process memory only. It deliberately excludes persistence, checkpointing, resume, distributed locks, and host service access.
/// </remarks>
public sealed class InMemoryExecutionStateStore : IExecutionStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, WorkflowExecutionStateSnapshot> _executions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkflowInvocationStateSnapshot> _invocations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NodeExecutionStateSnapshot> _nodes = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public WorkflowExecutionStateSnapshot CreateExecution(string executionId, string rootWorkflowId, string planId, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootWorkflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);

        lock (_gate)
        {
            if (_executions.ContainsKey(executionId))
            {
                throw new InvalidOperationException($"Execution '{executionId}' already exists.");
            }

            WorkflowExecutionStateSnapshot snapshot = new(executionId, rootWorkflowId, planId, ExecutionLifecycleState.Created, 1, timestamp, null, timestamp, null);
            _executions.Add(executionId, snapshot);
            return snapshot;
        }
    }

    /// <inheritdoc />
    public WorkflowInvocationStateSnapshot CreateInvocation(string executionId, string invocationId, string? parentInvocationId, string workflowId, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);

        lock (_gate)
        {
            if (_invocations.ContainsKey(invocationId))
            {
                throw new InvalidOperationException($"Invocation '{invocationId}' already exists.");
            }

            WorkflowInvocationStateSnapshot snapshot = new(executionId, invocationId, parentInvocationId, workflowId, ExecutionLifecycleState.Created, 1, timestamp, null, timestamp, null);
            _invocations.Add(invocationId, snapshot);
            return snapshot;
        }
    }

    /// <inheritdoc />
    public NodeExecutionStateSnapshot CreateNode(NodeExecutionIdentity identity, string nodeExecutionId, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeExecutionId);

        lock (_gate)
        {
            if (_nodes.ContainsKey(nodeExecutionId))
            {
                throw new InvalidOperationException($"Node execution '{nodeExecutionId}' already exists.");
            }

            NodeExecutionStateSnapshot snapshot = new(identity, nodeExecutionId, ExecutionLifecycleState.Created, 1, timestamp, null, timestamp, null);
            _nodes.Add(nodeExecutionId, snapshot);
            return snapshot;
        }
    }

    /// <inheritdoc />
    public WorkflowExecutionStateSnapshot TransitionExecution(string executionId, ExecutionLifecycleState next, DateTimeOffset timestamp, WorkflowExecutionResult? result = null)
    {
        lock (_gate)
        {
            WorkflowExecutionStateSnapshot current = GetExecutionLocked(executionId);
            ValidateTransition(current.State, next);
            WorkflowExecutionStateSnapshot snapshot = new(
                current.ExecutionId,
                current.RootWorkflowId,
                current.PlanId,
                next,
                current.Revision + 1,
                current.CreatedAt,
                next == ExecutionLifecycleState.Running && current.StartedAt is null ? timestamp : current.StartedAt,
                timestamp,
                next == ExecutionLifecycleState.Completed ? timestamp : current.CompletedAt,
                current.ActiveInvocationIds,
                result);
            _executions[executionId] = snapshot;
            return snapshot;
        }
    }

    /// <inheritdoc />
    public WorkflowInvocationStateSnapshot TransitionInvocation(string invocationId, ExecutionLifecycleState next, DateTimeOffset timestamp, WorkflowExecutionResult? result = null)
    {
        lock (_gate)
        {
            WorkflowInvocationStateSnapshot current = GetInvocationLocked(invocationId);
            ValidateTransition(current.State, next);
            WorkflowInvocationStateSnapshot snapshot = new(
                current.ExecutionId,
                current.InvocationId,
                current.ParentInvocationId,
                current.WorkflowId,
                next,
                current.Revision + 1,
                current.CreatedAt,
                next == ExecutionLifecycleState.Running && current.StartedAt is null ? timestamp : current.StartedAt,
                timestamp,
                next == ExecutionLifecycleState.Completed ? timestamp : current.CompletedAt,
                current.ActiveNodeExecutionIds,
                result);
            _invocations[invocationId] = snapshot;
            return snapshot;
        }
    }

    /// <inheritdoc />
    public NodeExecutionStateSnapshot TransitionNode(string nodeExecutionId, ExecutionLifecycleState next, DateTimeOffset timestamp, NodeExecutionResult? result = null)
    {
        lock (_gate)
        {
            NodeExecutionStateSnapshot current = GetNodeLocked(nodeExecutionId);
            ValidateTransition(current.State, next);
            NodeExecutionStateSnapshot snapshot = new(
                current.Identity,
                current.NodeExecutionId,
                next,
                current.Revision + 1,
                current.CreatedAt,
                next == ExecutionLifecycleState.Running && current.StartedAt is null ? timestamp : current.StartedAt,
                timestamp,
                next == ExecutionLifecycleState.Completed ? timestamp : current.CompletedAt,
                result);
            _nodes[nodeExecutionId] = snapshot;
            return snapshot;
        }
    }

    /// <inheritdoc />
    public WorkflowExecutionStateSnapshot GetExecution(string executionId)
    {
        lock (_gate)
        {
            return GetExecutionLocked(executionId);
        }
    }

    /// <inheritdoc />
    public WorkflowInvocationStateSnapshot GetInvocation(string invocationId)
    {
        lock (_gate)
        {
            return GetInvocationLocked(invocationId);
        }
    }

    /// <inheritdoc />
    public NodeExecutionStateSnapshot GetNode(string nodeExecutionId)
    {
        lock (_gate)
        {
            return GetNodeLocked(nodeExecutionId);
        }
    }

    private static void ValidateTransition(ExecutionLifecycleState current, ExecutionLifecycleState next)
    {
        bool valid = (current, next) switch
        {
            (ExecutionLifecycleState.Created, ExecutionLifecycleState.Ready) => true,
            (ExecutionLifecycleState.Ready, ExecutionLifecycleState.Running) => true,
            (ExecutionLifecycleState.Running, ExecutionLifecycleState.Suspended) => true,
            (ExecutionLifecycleState.Suspended, ExecutionLifecycleState.Running) => true,
            (ExecutionLifecycleState.Running, ExecutionLifecycleState.Cancelling) => true,
            (ExecutionLifecycleState.Ready, ExecutionLifecycleState.Cancelling) => true,
            (ExecutionLifecycleState.Suspended, ExecutionLifecycleState.Cancelling) => true,
            (ExecutionLifecycleState.Running, ExecutionLifecycleState.Completed) => true,
            (ExecutionLifecycleState.Cancelling, ExecutionLifecycleState.Completed) => true,
            _ => false,
        };

        if (!valid)
        {
            throw new InvalidOperationException($"{WorkflowRuntimeErrorCodes.InvalidRuntimeStateTransition}: Transition from {current} to {next} is not allowed.");
        }
    }

    private WorkflowExecutionStateSnapshot GetExecutionLocked(string executionId)
    {
        return _executions.TryGetValue(executionId, out WorkflowExecutionStateSnapshot? snapshot)
            ? snapshot
            : throw new KeyNotFoundException($"Execution '{executionId}' was not found.");
    }

    private WorkflowInvocationStateSnapshot GetInvocationLocked(string invocationId)
    {
        return _invocations.TryGetValue(invocationId, out WorkflowInvocationStateSnapshot? snapshot)
            ? snapshot
            : throw new KeyNotFoundException($"Invocation '{invocationId}' was not found.");
    }

    private NodeExecutionStateSnapshot GetNodeLocked(string nodeExecutionId)
    {
        return _nodes.TryGetValue(nodeExecutionId, out NodeExecutionStateSnapshot? snapshot)
            ? snapshot
            : throw new KeyNotFoundException($"Node execution '{nodeExecutionId}' was not found.");
    }
}
