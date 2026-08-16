using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Interaction;

/// <summary>
/// Describes one immutable host-neutral option for choice interactions.
/// </summary>
public sealed class WorkflowInteractionOption
{
    private readonly JsonNode? _value;

    /// <summary>
    /// Initializes an interaction option.
    /// </summary>
    /// <param name="id">The stable option identifier.</param>
    /// <param name="label">The presentation label for the option.</param>
    /// <param name="description">Optional presentation description.</param>
    /// <param name="value">Optional host-neutral JSON value cloned defensively.</param>
    public WorkflowInteractionOption(string id, string label, string? description = null, JsonNode? value = null)
    {
        Id = id;
        Label = label;
        Description = description;
        _value = value?.DeepClone();
    }

    /// <summary>
    /// Gets the stable option identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets presentation text for the option.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets optional presentation text for the option.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets a defensive clone of the optional host-neutral option value.
    /// </summary>
    public JsonNode? Value => _value?.DeepClone();
}
