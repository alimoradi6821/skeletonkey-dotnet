using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Interaction;

namespace SkeletonKey.Runtime.Interactions;

/// <summary>
/// Represents a host response used to continue a suspended in-memory interaction.
/// </summary>
public sealed class WorkflowInteractionContinuation
{
    private readonly JsonNode? _value;

    /// <summary>
    /// Initializes an interaction continuation response.
    /// </summary>
    /// <param name="continuationId">The session-local continuation identifier.</param>
    /// <param name="status">The host-neutral response status.</param>
    /// <param name="hasValue">Whether <paramref name="value" /> is explicitly supplied, including JSON null.</param>
    /// <param name="value">Optional response value.</param>
    public WorkflowInteractionContinuation(
        string continuationId,
        WorkflowInteractionResponseStatus status = WorkflowInteractionResponseStatus.Submitted,
        bool hasValue = true,
        JsonNode? value = null)
    {
        ContinuationId = continuationId;
        Status = status;
        HasValue = hasValue;
        _value = hasValue ? value?.DeepClone() : null;
    }

    /// <summary>Gets the session-local continuation identifier.</summary>
    public string ContinuationId { get; }

    /// <summary>Gets the host-neutral response status.</summary>
    public WorkflowInteractionResponseStatus Status { get; }

    /// <summary>Gets a value indicating whether a response value was supplied.</summary>
    public bool HasValue { get; }

    /// <summary>Gets a defensive clone of the optional response value.</summary>
    public JsonNode? Value => _value?.DeepClone();
}
