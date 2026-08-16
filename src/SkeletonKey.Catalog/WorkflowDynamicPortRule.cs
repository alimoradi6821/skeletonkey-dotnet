namespace SkeletonKey.Catalog;

/// <summary>
/// Describes a deterministic dynamic port derivation rule.
/// </summary>
public sealed class WorkflowDynamicPortRule
{
    /// <summary>
    /// Initializes a dynamic port rule.
    /// </summary>
    /// <param name="kind">The supported dynamic port rule kind.</param>
    /// <param name="direction">The direction of derived ports.</param>
    /// <param name="sourcePointer">The JSON Pointer to the parameter data that declares ports.</param>
    /// <param name="idPointer">The JSON Pointer relative to each source item that provides the port ID.</param>
    /// <param name="description">Optional human-readable rule description.</param>
    public WorkflowDynamicPortRule(
        WorkflowDynamicPortRuleKind kind,
        WorkflowPortDirection direction,
        string sourcePointer,
        string idPointer,
        string? description = null)
    {
        Kind = kind;
        Direction = direction;
        SourcePointer = sourcePointer;
        IdPointer = idPointer;
        Description = description;
    }

    /// <summary>
    /// Gets the supported dynamic port rule kind.
    /// </summary>
    public WorkflowDynamicPortRuleKind Kind { get; }

    /// <summary>
    /// Gets the direction of derived ports.
    /// </summary>
    public WorkflowPortDirection Direction { get; }

    /// <summary>
    /// Gets the JSON Pointer to the parameter data that declares ports.
    /// </summary>
    public string SourcePointer { get; }

    /// <summary>
    /// Gets the JSON Pointer relative to each source item that provides the port ID.
    /// </summary>
    public string IdPointer { get; }

    /// <summary>
    /// Gets an optional human-readable rule description.
    /// </summary>
    public string? Description { get; }
}
