using System.Collections.ObjectModel;
using SkeletonKey.Locators;

namespace SkeletonKey.Catalog;

/// <summary>
/// Describes a catalog-declared locator slot consumed from node parameters before handler execution.
/// </summary>
public sealed class NodeLocatorSlotDefinition
{
    private static readonly IReadOnlyList<LocatorCardinality> _emptyCardinalities = Array.AsReadOnly(Array.Empty<LocatorCardinality>());

    /// <summary>
    /// Initializes a locator slot definition.
    /// </summary>
    /// <param name="name">The case-sensitive node-local locator slot name.</param>
    /// <param name="parameterPointer">The RFC 6901 parameter pointer containing the `$locator` wrapper.</param>
    /// <param name="required">Whether the slot is required before handler execution.</param>
    /// <param name="usage">How the handler may use the resolved locator.</param>
    /// <param name="acceptedCardinalities">Accepted resolved locator cardinalities.</param>
    /// <param name="description">Optional human-readable slot description.</param>
    public NodeLocatorSlotDefinition(
        string name,
        string parameterPointer,
        bool required = true,
        LocatorUsageMode usage = LocatorUsageMode.Single,
        IReadOnlyList<LocatorCardinality>? acceptedCardinalities = null,
        string? description = null)
    {
        Name = name;
        ParameterPointer = parameterPointer;
        Required = required;
        Usage = usage;
        AcceptedCardinalities = acceptedCardinalities is null ? _emptyCardinalities : new ReadOnlyCollection<LocatorCardinality>([.. acceptedCardinalities]);
        Description = description;
    }

    /// <summary>Gets the case-sensitive node-local locator slot name.</summary>
    public string Name { get; }

    /// <summary>Gets the RFC 6901 parameter pointer containing the `$locator` wrapper.</summary>
    public string ParameterPointer { get; }

    /// <summary>Gets a value indicating whether the locator slot is required.</summary>
    public bool Required { get; }

    /// <summary>Gets how the handler may use the resolved locator.</summary>
    public LocatorUsageMode Usage { get; }

    /// <summary>Gets accepted resolved locator cardinalities.</summary>
    public IReadOnlyList<LocatorCardinality> AcceptedCardinalities { get; }

    /// <summary>Gets optional human-readable slot description.</summary>
    public string? Description { get; }
}
