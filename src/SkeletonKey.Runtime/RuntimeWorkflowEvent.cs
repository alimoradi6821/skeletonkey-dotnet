using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;

namespace SkeletonKey.Runtime;

/// <summary>
/// Represents a runtime-owned ordered workflow execution event.
/// </summary>
/// <remarks>
/// The runtime owns sequence numbers, event IDs, timestamps, execution identity, invocation identity, node enrichment, and payload cloning.
/// Handlers can request log, progress, and output observations but cannot select these runtime-owned fields.
/// </remarks>
public sealed class RuntimeWorkflowEvent : WorkflowEvent
{
    private readonly JsonObject? _data;

    /// <summary>
    /// Initializes a new runtime workflow event.
    /// </summary>
    /// <param name="eventId">The runtime-owned stable event identifier.</param>
    /// <param name="sequence">The one-based root execution event sequence number.</param>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="invocationId">The workflow invocation identifier.</param>
    /// <param name="parentInvocationId">The optional parent workflow invocation identifier.</param>
    /// <param name="timestampUtc">The runtime-owned UTC timestamp.</param>
    /// <param name="kind">The runtime event kind.</param>
    /// <param name="message">Optional human-readable event message.</param>
    /// <param name="nodeId">Optional node identifier for node-scoped events.</param>
    /// <param name="data">Optional host-neutral event data.</param>
    public RuntimeWorkflowEvent(
        string eventId,
        long sequence,
        string executionId,
        string workflowId,
        string invocationId,
        string? parentInvocationId,
        DateTimeOffset timestampUtc,
        RuntimeWorkflowEventKind kind,
        string? message = null,
        string? nodeId = null,
        JsonObject? data = null)
        : base(eventId, executionId, workflowId, invocationId, parentInvocationId, timestampUtc)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Runtime event sequence numbers are one-based.");
        }

        Sequence = sequence;
        Kind = kind;
        Message = message;
        NodeId = nodeId;
        _data = data is null ? null : (JsonObject)data.DeepClone();
    }

    /// <summary>
    /// Gets the one-based root execution event sequence number.
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// Gets the runtime event kind.
    /// </summary>
    public RuntimeWorkflowEventKind Kind { get; }

    /// <summary>
    /// Gets optional human-readable event message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the optional node identifier for node-scoped events.
    /// </summary>
    public string? NodeId { get; }

    /// <summary>
    /// Gets a defensive copy of optional host-neutral event data.
    /// </summary>
    public JsonObject? Data => _data is null ? null : (JsonObject)_data.DeepClone();
}
