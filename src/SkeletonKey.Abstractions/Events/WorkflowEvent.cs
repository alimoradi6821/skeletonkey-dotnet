namespace SkeletonKey.Abstractions.Events;

/// <summary>
/// Represents a host-neutral workflow execution event.
/// </summary>
public abstract class WorkflowEvent
{
    /// <summary>
    /// Initializes a new workflow event.
    /// </summary>
    /// <param name="eventId">The stable event identifier.</param>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="invocationId">The workflow invocation identifier.</param>
    /// <param name="parentInvocationId">The optional parent workflow invocation identifier.</param>
    /// <param name="timestampUtc">The UTC event timestamp.</param>
    protected WorkflowEvent(
        string eventId,
        string executionId,
        string workflowId,
        string invocationId,
        string? parentInvocationId,
        DateTimeOffset timestampUtc)
    {
        EventId = eventId;
        ExecutionId = executionId;
        WorkflowId = workflowId;
        InvocationId = invocationId;
        ParentInvocationId = parentInvocationId;
        TimestampUtc = timestampUtc;
    }

    /// <summary>
    /// Gets the stable event identifier.
    /// </summary>
    public string EventId { get; }

    /// <summary>
    /// Gets the workflow execution identifier.
    /// </summary>
    public string ExecutionId { get; }

    /// <summary>
    /// Gets the workflow identifier.
    /// </summary>
    public string WorkflowId { get; }

    /// <summary>
    /// Gets the workflow invocation identifier.
    /// </summary>
    public string InvocationId { get; }

    /// <summary>
    /// Gets the optional parent workflow invocation identifier.
    /// </summary>
    public string? ParentInvocationId { get; }

    /// <summary>
    /// Gets the UTC event timestamp.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; }
}
