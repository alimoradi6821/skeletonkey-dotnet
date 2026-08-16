using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Execution;

/// <summary>
/// Represents an immutable snapshot of one node execution attempt state.
/// </summary>
/// <remarks>
/// Revisions and timestamps are supplied by a future runtime. This snapshot does not transition or execute state.
/// </remarks>
public sealed class NodeExecutionStateSnapshot
{
    /// <summary>
    /// Initializes a new node execution state snapshot.
    /// </summary>
    /// <param name="identity">The exact node execution attempt identity.</param>
    /// <param name="nodeExecutionId">The runtime-supplied node execution attempt identifier.</param>
    /// <param name="state">The lifecycle state at this revision.</param>
    /// <param name="revision">The runtime-supplied monotonically advancing state revision.</param>
    /// <param name="createdAt">The runtime-supplied creation timestamp.</param>
    /// <param name="startedAt">The optional runtime-supplied start timestamp.</param>
    /// <param name="updatedAt">The runtime-supplied update timestamp for this revision.</param>
    /// <param name="completedAt">The optional runtime-supplied completion timestamp.</param>
    /// <param name="result">The optional terminal node execution result.</param>
    public NodeExecutionStateSnapshot(
        NodeExecutionIdentity identity,
        string nodeExecutionId,
        ExecutionLifecycleState state,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? completedAt,
        NodeExecutionResult? result = null)
    {
        Identity = identity;
        NodeExecutionId = nodeExecutionId;
        State = state;
        Revision = revision;
        CreatedAt = createdAt;
        StartedAt = startedAt;
        UpdatedAt = updatedAt;
        CompletedAt = completedAt;
        Result = result;
    }

    /// <summary>
    /// Gets the exact node execution attempt identity.
    /// </summary>
    public NodeExecutionIdentity Identity { get; }

    /// <summary>
    /// Gets the runtime-supplied node execution attempt identifier.
    /// </summary>
    public string NodeExecutionId { get; }

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
    /// Gets the optional terminal node execution result.
    /// </summary>
    public NodeExecutionResult? Result { get; }
}
