namespace SkeletonKey.Execution;

/// <summary>
/// Represents an immutable runtime-owned observation of a lifecycle state transition.
/// </summary>
/// <remarks>
/// This contract records a transition observation only. It does not validate, persist, dispatch, or mutate execution state.
/// </remarks>
public sealed class ExecutionStateTransition
{
    /// <summary>
    /// Initializes a new state transition observation.
    /// </summary>
    /// <param name="scope">The scope whose lifecycle changed.</param>
    /// <param name="executionId">The root execution identifier.</param>
    /// <param name="invocationId">The optional invocation identifier for invocation or node scopes.</param>
    /// <param name="nodeExecutionId">The optional node execution identifier for node scopes.</param>
    /// <param name="previous">The previous lifecycle state.</param>
    /// <param name="current">The current lifecycle state.</param>
    /// <param name="revision">The runtime-supplied revision associated with the transition.</param>
    /// <param name="timestamp">The runtime-supplied transition timestamp.</param>
    /// <param name="reasonCode">An optional host-neutral reason code.</param>
    /// <param name="message">An optional human-readable message.</param>
    public ExecutionStateTransition(
        ExecutionScopeKind scope,
        string executionId,
        string? invocationId,
        string? nodeExecutionId,
        ExecutionLifecycleState previous,
        ExecutionLifecycleState current,
        long revision,
        DateTimeOffset timestamp,
        string? reasonCode = null,
        string? message = null)
    {
        Scope = scope;
        ExecutionId = executionId;
        InvocationId = invocationId;
        NodeExecutionId = nodeExecutionId;
        Previous = previous;
        Current = current;
        Revision = revision;
        Timestamp = timestamp;
        ReasonCode = reasonCode;
        Message = message;
    }

    /// <summary>
    /// Gets the scope whose lifecycle changed.
    /// </summary>
    public ExecutionScopeKind Scope { get; }

    /// <summary>
    /// Gets the root execution identifier.
    /// </summary>
    public string ExecutionId { get; }

    /// <summary>
    /// Gets the optional invocation identifier.
    /// </summary>
    public string? InvocationId { get; }

    /// <summary>
    /// Gets the optional node execution identifier.
    /// </summary>
    public string? NodeExecutionId { get; }

    /// <summary>
    /// Gets the previous lifecycle state.
    /// </summary>
    public ExecutionLifecycleState Previous { get; }

    /// <summary>
    /// Gets the current lifecycle state.
    /// </summary>
    public ExecutionLifecycleState Current { get; }

    /// <summary>
    /// Gets the runtime-supplied revision associated with the transition.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets the runtime-supplied transition timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets an optional host-neutral reason code.
    /// </summary>
    public string? ReasonCode { get; }

    /// <summary>
    /// Gets an optional human-readable message.
    /// </summary>
    public string? Message { get; }
}
