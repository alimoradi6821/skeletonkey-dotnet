using SkeletonKey.Catalog;

namespace SkeletonKey.Analysis;

/// <summary>
/// Describes an immutable effective node port after static and dynamic catalog metadata are combined.
/// </summary>
/// <remarks>
/// Port identity is ordinal and case-sensitive. Dynamic ports are derived only from literal workflow
/// parameter data and do not execute bindings, expressions, handlers, or runtime state.
/// </remarks>
public sealed class WorkflowEffectivePort
{
    private static readonly IReadOnlyList<string> _defaultRoles = Array.AsReadOnly(["control"]);

    /// <summary>
    /// Initializes an effective port.
    /// </summary>
    /// <param name="id">The ordinal case-sensitive port identifier.</param>
    /// <param name="direction">The port direction.</param>
    /// <param name="required">Whether the port is required by the node contract.</param>
    /// <param name="allowsMultiple">Whether multiple compatible connections are statically allowed.</param>
    /// <param name="roles">Ordered role identifiers used for deterministic compatibility analysis.</param>
    /// <param name="origin">Whether the port is static or dynamic.</param>
    /// <param name="originPath">Optional JSON Pointer to the catalog or workflow parameter source.</param>
    /// <param name="sourceRuleKind">Optional dynamic-port rule kind that produced this port.</param>
    public WorkflowEffectivePort(
        string id,
        WorkflowPortDirection direction,
        bool required = false,
        bool allowsMultiple = false,
        IReadOnlyList<string>? roles = null,
        WorkflowEffectivePortOrigin origin = WorkflowEffectivePortOrigin.Static,
        string? originPath = null,
        WorkflowDynamicPortRuleKind? sourceRuleKind = null)
    {
        Id = id;
        Direction = direction;
        Required = required;
        AllowsMultiple = allowsMultiple;
        Roles = roles is null ? _defaultRoles : Array.AsReadOnly([.. roles]);
        Origin = origin;
        OriginPath = originPath;
        SourceRuleKind = sourceRuleKind;
    }

    /// <summary>Gets the ordinal case-sensitive port identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the port direction.</summary>
    public WorkflowPortDirection Direction { get; }

    /// <summary>Gets a value indicating whether the port is required by the node contract.</summary>
    public bool Required { get; }

    /// <summary>Gets a value indicating whether multiple compatible connections are statically allowed.</summary>
    public bool AllowsMultiple { get; }

    /// <summary>Gets ordered role identifiers used for compatibility analysis.</summary>
    public IReadOnlyList<string> Roles { get; }

    /// <summary>Gets whether the port is static or dynamic.</summary>
    public WorkflowEffectivePortOrigin Origin { get; }

    /// <summary>Gets the optional JSON Pointer to the catalog or workflow parameter source.</summary>
    public string? OriginPath { get; }

    /// <summary>Gets the optional dynamic-port rule kind that produced this port.</summary>
    public WorkflowDynamicPortRuleKind? SourceRuleKind { get; }
}
