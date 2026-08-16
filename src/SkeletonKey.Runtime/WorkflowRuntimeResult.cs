using System.Collections.ObjectModel;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;

namespace SkeletonKey.Runtime;

/// <summary>
/// Represents the immutable result returned by a workflow runtime.
/// </summary>
/// <remarks>
/// The wrapper preserves the existing workflow result contract while also exposing runtime-owned state snapshots and node results.
/// It does not imply persistence or durable resume support.
/// </remarks>
public sealed class WorkflowRuntimeResult
{
    private readonly IReadOnlyList<NodeExecutionResult> _nodeResults;
    private readonly IReadOnlyList<NodeExecutionStateSnapshot> _nodeSnapshots;

    /// <summary>
    /// Initializes a new runtime result.
    /// </summary>
    /// <param name="result">The final host-neutral workflow execution result.</param>
    /// <param name="executionSnapshot">The final root execution state snapshot.</param>
    /// <param name="invocationSnapshot">The final root invocation state snapshot, when state was initialized.</param>
    /// <param name="nodeResults">Terminal node execution results in deterministic scheduler order.</param>
    /// <param name="nodeSnapshots">Final node execution state snapshots in deterministic scheduler order.</param>
    public WorkflowRuntimeResult(
        WorkflowExecutionResult result,
        WorkflowExecutionStateSnapshot? executionSnapshot = null,
        WorkflowInvocationStateSnapshot? invocationSnapshot = null,
        IReadOnlyList<NodeExecutionResult>? nodeResults = null,
        IReadOnlyList<NodeExecutionStateSnapshot>? nodeSnapshots = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
        ExecutionSnapshot = executionSnapshot;
        InvocationSnapshot = invocationSnapshot;
        _nodeResults = nodeResults is null ? Array.AsReadOnly(Array.Empty<NodeExecutionResult>()) : Array.AsReadOnly([.. nodeResults]);
        _nodeSnapshots = nodeSnapshots is null ? Array.AsReadOnly(Array.Empty<NodeExecutionStateSnapshot>()) : Array.AsReadOnly([.. nodeSnapshots]);
    }

    /// <summary>
    /// Gets the final host-neutral workflow execution result.
    /// </summary>
    public WorkflowExecutionResult Result { get; }

    /// <summary>
    /// Gets the final root execution state snapshot, when state was initialized.
    /// </summary>
    public WorkflowExecutionStateSnapshot? ExecutionSnapshot { get; }

    /// <summary>
    /// Gets the final root invocation state snapshot, when state was initialized.
    /// </summary>
    public WorkflowInvocationStateSnapshot? InvocationSnapshot { get; }

    /// <summary>
    /// Gets terminal node execution results in deterministic scheduler order.
    /// </summary>
    public IReadOnlyList<NodeExecutionResult> NodeResults => new ReadOnlyCollection<NodeExecutionResult>([.. _nodeResults]);

    /// <summary>
    /// Gets final node execution state snapshots in deterministic scheduler order.
    /// </summary>
    public IReadOnlyList<NodeExecutionStateSnapshot> NodeSnapshots => new ReadOnlyCollection<NodeExecutionStateSnapshot>([.. _nodeSnapshots]);
}
