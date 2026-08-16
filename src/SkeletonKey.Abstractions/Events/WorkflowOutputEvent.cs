using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Events;

/// <summary>
/// Represents a host-neutral streamed workflow output record.
/// </summary>
/// <remarks>
/// Streamed output payloads are delivered as events and are not required to be duplicated in final workflow outputs.
/// JSON payload values are defensively cloned.
/// </remarks>
public sealed class WorkflowOutputEvent : WorkflowEvent
{
    private readonly JsonNode? _payload;

    /// <summary>
    /// Initializes a new workflow output event.
    /// </summary>
    /// <param name="eventId">The stable event identifier.</param>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="invocationId">The workflow invocation identifier.</param>
    /// <param name="parentInvocationId">The optional parent workflow invocation identifier.</param>
    /// <param name="timestampUtc">The UTC event timestamp.</param>
    /// <param name="outputName">The workflow output declaration name.</param>
    /// <param name="channel">The stream channel name.</param>
    /// <param name="sequence">The zero-based event sequence for this output stream.</param>
    /// <param name="payload">Optional JSON output payload.</param>
    public WorkflowOutputEvent(
        string eventId,
        string executionId,
        string workflowId,
        string invocationId,
        string? parentInvocationId,
        DateTimeOffset timestampUtc,
        string outputName,
        string channel,
        long sequence,
        JsonNode? payload = null)
        : base(eventId, executionId, workflowId, invocationId, parentInvocationId, timestampUtc)
    {
        OutputName = outputName;
        Channel = channel;
        Sequence = sequence;
        _payload = payload?.DeepClone();
    }

    /// <summary>
    /// Gets the workflow output declaration name.
    /// </summary>
    public string OutputName { get; }

    /// <summary>
    /// Gets the stream channel name.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// Gets the zero-based event sequence for this output stream.
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// Gets a defensive copy of the optional JSON output payload.
    /// </summary>
    public JsonNode? Payload => _payload?.DeepClone();
}
