using System.Text.Json.Nodes;

namespace SkeletonKey.Workflow.Inputs;

/// <summary>
/// Describes a declared workflow input and its optional default value.
/// </summary>
public sealed class WorkflowInputDefinition
{
    private readonly JsonNode? _defaultValue;

    /// <summary>
    /// Initializes a new workflow input definition.
    /// </summary>
    /// <param name="type">The declared input value type.</param>
    /// <param name="required">Whether callers must provide the input.</param>
    /// <param name="defaultValue">The optional JSON default value.</param>
    /// <param name="description">Optional human-readable input help text.</param>
    /// <param name="hasDefault">Whether the input explicitly declares a default value, including JSON null.</param>
    public WorkflowInputDefinition(
        WorkflowInputType type,
        bool required = false,
        JsonNode? defaultValue = null,
        string? description = null,
        bool hasDefault = false)
    {
        Type = type;
        Required = required;
        HasDefault = hasDefault || defaultValue is not null;
        _defaultValue = defaultValue?.DeepClone();
        Description = description;
    }

    /// <summary>
    /// Gets the declared input value type.
    /// </summary>
    public WorkflowInputType Type { get; }

    /// <summary>
    /// Gets a value indicating whether callers must provide this input.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets a defensive copy of the optional JSON default value.
    /// </summary>
    public JsonNode? Default => _defaultValue?.DeepClone();

    /// <summary>
    /// Gets a value indicating whether a default value was explicitly declared.
    /// </summary>
    public bool HasDefault { get; }

    /// <summary>
    /// Gets optional human-readable input help text.
    /// </summary>
    public string? Description { get; }
}
