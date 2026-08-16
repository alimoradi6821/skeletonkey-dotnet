using System.Collections.ObjectModel;

namespace SkeletonKey.Catalog;

/// <summary>
/// Describes a catalog-declared resource requirement for a node type.
/// </summary>
public sealed class WorkflowNodeResourceRequirement
{
    private static readonly IReadOnlyList<string> _emptyCapabilities = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>
    /// Initializes a catalog resource requirement.
    /// </summary>
    /// <param name="name">The node-local resource slot name.</param>
    /// <param name="kind">The required workflow resource kind.</param>
    /// <param name="required">Whether the resource slot is required before execution.</param>
    /// <param name="capabilities">Ordered capability identifiers required by the node.</param>
    /// <param name="description">Optional human-readable requirement description.</param>
    public WorkflowNodeResourceRequirement(
        string name,
        string kind,
        bool required = true,
        IReadOnlyList<string>? capabilities = null,
        string? description = null)
    {
        Name = name;
        Kind = kind;
        Required = required;
        Capabilities = capabilities is null ? _emptyCapabilities : Array.AsReadOnly([.. capabilities]);
        Description = description;
    }

    /// <summary>
    /// Gets the node-local resource slot name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the required workflow resource kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the resource slot is required before execution.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets ordered capability identifiers required by the node.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Gets an optional human-readable requirement description.
    /// </summary>
    public string? Description { get; }
}
