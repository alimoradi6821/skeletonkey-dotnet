using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Interaction;

/// <summary>
/// Describes an immutable host-neutral response to a human interaction request.
/// </summary>
public sealed class WorkflowInteractionResponse
{
    private readonly JsonNode? _value;

    /// <summary>
    /// Initializes an interaction response.
    /// </summary>
    /// <param name="requestId">The interaction request identifier.</param>
    /// <param name="status">The host-neutral response status.</param>
    /// <param name="hasValue">Whether <paramref name="value" /> was explicitly supplied, including JSON null.</param>
    /// <param name="value">Optional response JSON value cloned defensively.</param>
    /// <param name="respondedAt">The response timestamp.</param>
    public WorkflowInteractionResponse(
        string requestId,
        WorkflowInteractionResponseStatus status,
        bool hasValue,
        JsonNode? value,
        DateTimeOffset respondedAt)
    {
        RequestId = requestId;
        Status = status;
        HasValue = hasValue;
        _value = hasValue ? value?.DeepClone() : null;
        RespondedAt = respondedAt;
    }

    /// <summary>Gets the interaction request identifier.</summary>
    public string RequestId { get; }

    /// <summary>Gets the host-neutral response status.</summary>
    public WorkflowInteractionResponseStatus Status { get; }

    /// <summary>Gets a value indicating whether a response value was explicitly supplied.</summary>
    public bool HasValue { get; }

    /// <summary>Gets a defensive clone of the optional response value.</summary>
    public JsonNode? Value => _value?.DeepClone();

    /// <summary>Gets the response timestamp.</summary>
    public DateTimeOffset RespondedAt { get; }
}
