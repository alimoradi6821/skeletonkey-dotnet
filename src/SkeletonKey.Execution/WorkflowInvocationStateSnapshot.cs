using System.Collections.ObjectModel;
using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Execution;

/// <summary>
/// Represents an immutable snapshot of one workflow invocation state.
/// </summary>
/// <remarks>
/// Revisions and timestamps are supplied by a future runtime. This snapshot exposes no mutation or execution behavior.
/// </remarks>
public sealed class WorkflowInvocationStateSnapshot
{
    private readonly IReadOnlyList<string> _activeNodeExecutionIds;

    /// <summary>
    /// Initializes a new workflow invocation state snapshot.
    /// </summary>
    /// <param name="executionId">The root execution identifier.</param>
    /// <param name="invocationId">The workflow invocation identifier.</param>
    /// <param name="parentInvocationId">The optional parent invocation identifier.</param>
    /// <param name="workflowId">The invoked workflow identifier.</param>
    /// <param name="state">The lifecycle state at this revision.</param>
    /// <param name="revision">The runtime-supplied monotonically advancing state revision.</param>
    /// <param name="createdAt">The runtime-supplied creation timestamp.</param>
    /// <param name="startedAt">The optional runtime-supplied start timestamp.</param>
    /// <param name="updatedAt">The runtime-supplied update timestamp for this revision.</param>
    /// <param name="completedAt">The optional runtime-supplied completion timestamp.</param>
    /// <param name="activeNodeExecutionIds">The active node execution identifiers at this revision.</param>
    /// <param name="result">The optional terminal workflow invocation result.</param>
    public WorkflowInvocationStateSnapshot(
        string executionId,
        string invocationId,
        string? parentInvocationId,
        string workflowId,
        ExecutionLifecycleState state,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? completedAt,
        IReadOnlyList<string>? activeNodeExecutionIds = null,
        WorkflowExecutionResult? result = null)
    {
        ExecutionId = executionId;
        InvocationId = invocationId;
        ParentInvocationId = parentInvocationId;
        WorkflowId = workflowId;
        State = state;
        Revision = revision;
        CreatedAt = createdAt;
        StartedAt = startedAt;
        UpdatedAt = updatedAt;
        CompletedAt = completedAt;
        _activeNodeExecutionIds = activeNodeExecutionIds is null ? Array.AsReadOnly(Array.Empty<string>()) : new ReadOnlyCollection<string>([.. activeNodeExecutionIds]);
        Result = result;
    }

    /// <summary>
    /// Gets the root execution identifier.
    /// </summary>
    public string ExecutionId { get; }

    /// <summary>
    /// Gets the workflow invocation identifier.
    /// </summary>
    public string InvocationId { get; }

    /// <summary>
    /// Gets the optional parent invocation identifier.
    /// </summary>
    public string? ParentInvocationId { get; }

    /// <summary>
    /// Gets the invoked workflow identifier.
    /// </summary>
    public string WorkflowId { get; }

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
    /// Gets a defensive copy of active node execution identifiers.
    /// </summary>
    public IReadOnlyList<string> ActiveNodeExecutionIds => new ReadOnlyCollection<string>([.. _activeNodeExecutionIds]);

    /// <summary>
    /// Gets the optional terminal workflow invocation result.
    /// </summary>
    public WorkflowExecutionResult? Result { get; }
}
