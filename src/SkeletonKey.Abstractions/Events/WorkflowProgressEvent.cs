using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Events;

/// <summary>
/// Represents a host-neutral workflow progress event.
/// </summary>
/// <remarks>
/// JSON progress data is defensively cloned on input and output.
/// </remarks>
public sealed class WorkflowProgressEvent : WorkflowEvent
{
    private readonly JsonObject? _data;

    /// <summary>
    /// Initializes a new workflow progress event.
    /// </summary>
    /// <param name="eventId">The stable event identifier.</param>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="invocationId">The workflow invocation identifier.</param>
    /// <param name="parentInvocationId">The optional parent workflow invocation identifier.</param>
    /// <param name="timestampUtc">The UTC event timestamp.</param>
    /// <param name="completedNodes">The number of completed nodes.</param>
    /// <param name="totalNodes">The optional total number of nodes expected for this execution.</param>
    /// <param name="currentNodeId">The optional current node identifier.</param>
    /// <param name="message">Optional progress message.</param>
    /// <param name="data">Optional JSON progress data.</param>
    public WorkflowProgressEvent(
        string eventId,
        string executionId,
        string workflowId,
        string invocationId,
        string? parentInvocationId,
        DateTimeOffset timestampUtc,
        int completedNodes,
        int? totalNodes = null,
        string? currentNodeId = null,
        string? message = null,
        JsonObject? data = null)
        : base(eventId, executionId, workflowId, invocationId, parentInvocationId, timestampUtc)
    {
        CompletedNodes = completedNodes;
        TotalNodes = totalNodes;
        CurrentNodeId = currentNodeId;
        Message = message;
        _data = data is null ? null : (JsonObject)data.DeepClone();
    }

    /// <summary>
    /// Gets the number of completed nodes.
    /// </summary>
    public int CompletedNodes { get; }

    /// <summary>
    /// Gets the optional total number of nodes expected for this execution.
    /// </summary>
    public int? TotalNodes { get; }

    /// <summary>
    /// Gets the optional current node identifier.
    /// </summary>
    public string? CurrentNodeId { get; }

    /// <summary>
    /// Gets the optional progress message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets a defensive copy of optional JSON progress data.
    /// </summary>
    public JsonObject? Data => _data is null ? null : (JsonObject)_data.DeepClone();
}
