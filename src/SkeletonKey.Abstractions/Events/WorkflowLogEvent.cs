using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Events;

/// <summary>
/// Represents a host-neutral workflow log event.
/// </summary>
/// <remarks>
/// JSON log data is defensively cloned on input and output.
/// </remarks>
public sealed class WorkflowLogEvent : WorkflowEvent
{
    private readonly JsonObject? _data;

    /// <summary>
    /// Initializes a new workflow log event.
    /// </summary>
    /// <param name="eventId">The stable event identifier.</param>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="invocationId">The workflow invocation identifier.</param>
    /// <param name="parentInvocationId">The optional parent workflow invocation identifier.</param>
    /// <param name="timestampUtc">The UTC event timestamp.</param>
    /// <param name="level">The host-neutral log severity.</param>
    /// <param name="message">The log message.</param>
    /// <param name="nodeId">The optional node identifier associated with the log event.</param>
    /// <param name="data">Optional JSON log data.</param>
    public WorkflowLogEvent(
        string eventId,
        string executionId,
        string workflowId,
        string invocationId,
        string? parentInvocationId,
        DateTimeOffset timestampUtc,
        WorkflowLogLevel level,
        string message,
        string? nodeId = null,
        JsonObject? data = null)
        : base(eventId, executionId, workflowId, invocationId, parentInvocationId, timestampUtc)
    {
        Level = level;
        Message = message;
        NodeId = nodeId;
        _data = data is null ? null : (JsonObject)data.DeepClone();
    }

    /// <summary>
    /// Gets the host-neutral log severity.
    /// </summary>
    public WorkflowLogLevel Level { get; }

    /// <summary>
    /// Gets the log message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional node identifier associated with the log event.
    /// </summary>
    public string? NodeId { get; }

    /// <summary>
    /// Gets a defensive copy of optional JSON log data.
    /// </summary>
    public JsonObject? Data => _data is null ? null : (JsonObject)_data.DeepClone();
}
