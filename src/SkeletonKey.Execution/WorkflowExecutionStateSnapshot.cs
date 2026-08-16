using System.Collections.ObjectModel;
using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Execution;

/// <summary>
/// Represents an immutable snapshot of the root workflow execution state.
/// </summary>
/// <remarks>
/// Revisions and timestamps are supplied by a future runtime. This snapshot exposes no mutation or clock access.
/// </remarks>
public sealed class WorkflowExecutionStateSnapshot
{
    private readonly IReadOnlyList<string> _activeInvocationIds;

    /// <summary>
    /// Initializes a new workflow execution state snapshot.
    /// </summary>
    /// <param name="executionId">The root execution identifier.</param>
    /// <param name="rootWorkflowId">The root workflow identifier.</param>
    /// <param name="planId">The execution plan identifier.</param>
    /// <param name="state">The lifecycle state at this revision.</param>
    /// <param name="revision">The runtime-supplied monotonically advancing state revision.</param>
    /// <param name="createdAt">The runtime-supplied creation timestamp.</param>
    /// <param name="startedAt">The optional runtime-supplied start timestamp.</param>
    /// <param name="updatedAt">The runtime-supplied update timestamp for this revision.</param>
    /// <param name="completedAt">The optional runtime-supplied completion timestamp.</param>
    /// <param name="activeInvocationIds">The active invocation identifiers at this revision.</param>
    /// <param name="result">The optional terminal workflow execution result.</param>
    public WorkflowExecutionStateSnapshot(
        string executionId,
        string rootWorkflowId,
        string planId,
        ExecutionLifecycleState state,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? completedAt,
        IReadOnlyList<string>? activeInvocationIds = null,
        WorkflowExecutionResult? result = null)
    {
        ExecutionId = executionId;
        RootWorkflowId = rootWorkflowId;
        PlanId = planId;
        State = state;
        Revision = revision;
        CreatedAt = createdAt;
        StartedAt = startedAt;
        UpdatedAt = updatedAt;
        CompletedAt = completedAt;
        _activeInvocationIds = activeInvocationIds is null ? Array.AsReadOnly(Array.Empty<string>()) : new ReadOnlyCollection<string>([.. activeInvocationIds]);
        Result = result;
    }

    /// <summary>
    /// Gets the root execution identifier.
    /// </summary>
    public string ExecutionId { get; }

    /// <summary>
    /// Gets the root workflow identifier.
    /// </summary>
    public string RootWorkflowId { get; }

    /// <summary>
    /// Gets the execution plan identifier.
    /// </summary>
    public string PlanId { get; }

    /// <summary>
    /// Gets the lifecycle state at this revision.
    /// </summary>
    public ExecutionLifecycleState State { get; }

    /// <summary>
    /// Gets the runtime-supplied monotonically advancing state revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets the runtime-supplied creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the optional runtime-supplied start timestamp.
    /// </summary>
    public DateTimeOffset? StartedAt { get; }

    /// <summary>
    /// Gets the runtime-supplied update timestamp for this revision.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>
    /// Gets the optional runtime-supplied completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; }

    /// <summary>
    /// Gets a defensive copy of active invocation identifiers.
    /// </summary>
    public IReadOnlyList<string> ActiveInvocationIds => new ReadOnlyCollection<string>([.. _activeInvocationIds]);

    /// <summary>
    /// Gets the optional terminal workflow execution result.
    /// </summary>
    public WorkflowExecutionResult? Result { get; }
}
