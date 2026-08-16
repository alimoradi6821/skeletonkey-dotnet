using System.Text.Json.Nodes;

namespace SkeletonKey.Catalog;

/// <summary>
/// Describes one catalog-declared node port.
/// </summary>
public sealed class WorkflowPortDefinition
{
    private static readonly IReadOnlyList<string> _defaultRoles = Array.AsReadOnly(["control"]);
    private readonly JsonObject? _schema;

    /// <summary>
    /// Initializes a catalog port definition.
    /// </summary>
    /// <param name="name">The port name used by workflow endpoints.</param>
    /// <param name="direction">Whether this is an input or output port.</param>
    /// <param name="required">Whether the port is required by the node definition.</param>
    /// <param name="allowsMultiple">Whether multiple connections may target or originate from this port.</param>
    /// <param name="valueType">Optional provider-neutral value type hint.</param>
    /// <param name="schema">Optional JSON schema fragment for the port value.</param>
    /// <param name="description">Optional human-readable port description.</param>
    /// <param name="roles">Ordered role identifiers used by catalog-aware connection compatibility analysis.</param>
    public WorkflowPortDefinition(
        string name,
        WorkflowPortDirection direction,
        bool required = false,
        bool allowsMultiple = false,
        string? valueType = null,
        JsonObject? schema = null,
        string? description = null,
        IReadOnlyList<string>? roles = null)
    {
        Name = name;
        Direction = direction;
        Required = required;
        AllowsMultiple = allowsMultiple;
        ValueType = valueType;
        _schema = schema is null ? null : (JsonObject)schema.DeepClone();
        Description = description;
        Roles = roles is null ? _defaultRoles : Array.AsReadOnly([.. roles]);
    }

    /// <summary>
    /// Gets the port name used by workflow endpoints.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether this is an input or output port.
    /// </summary>
    public WorkflowPortDirection Direction { get; }

    /// <summary>
    /// Gets a value indicating whether the port is required by the node definition.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets a value indicating whether multiple connections may target or originate from this port.
    /// </summary>
    public bool AllowsMultiple { get; }

    /// <summary>
    /// Gets an optional provider-neutral value type hint.
    /// </summary>
    public string? ValueType { get; }

    /// <summary>
    /// Gets a defensive clone of the optional JSON schema fragment for the port value.
    /// </summary>
    public JsonObject? Schema => _schema is null ? null : (JsonObject)_schema.DeepClone();

    /// <summary>
    /// Gets an optional human-readable port description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets ordered role identifiers used by catalog-aware connection compatibility analysis.
    /// </summary>
    public IReadOnlyList<string> Roles { get; }
}
