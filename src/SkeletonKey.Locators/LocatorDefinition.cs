using System.Collections.ObjectModel;

namespace SkeletonKey.Locators;

/// <summary>
/// Describes one immutable semantic UI target with ordered locator strategy fallbacks.
/// </summary>
public sealed class LocatorDefinition
{
    private static readonly IReadOnlyList<LocatorStrategy> _emptyStrategies = Array.AsReadOnly(Array.Empty<LocatorStrategy>());

    /// <summary>
    /// Initializes a locator definition.
    /// </summary>
    /// <param name="description">Optional human-readable description.</param>
    /// <param name="within">Optional local locator ID used to scope this locator.</param>
    /// <param name="cardinality">The expected match cardinality for future provider diagnostics.</param>
    /// <param name="strategies">Ordered semantic-first strategies; later entries are fallbacks.</param>
    public LocatorDefinition(
        string? description = null,
        string? within = null,
        LocatorCardinality cardinality = LocatorCardinality.One,
        IReadOnlyList<LocatorStrategy>? strategies = null)
    {
        Description = description;
        Within = within;
        Cardinality = cardinality;
        Strategies = strategies is null ? _emptyStrategies : new ReadOnlyCollection<LocatorStrategy>([.. strategies]);
    }

    /// <summary>Gets optional human-readable description text.</summary>
    public string? Description { get; }

    /// <summary>Gets the optional local locator ID used as scope before this locator is resolved.</summary>
    public string? Within { get; }

    /// <summary>Gets the expected match cardinality for future resolution and diagnostics.</summary>
    public LocatorCardinality Cardinality { get; }

    /// <summary>Gets ordered strategies where the first is preferred and later strategies are fallbacks.</summary>
    public IReadOnlyList<LocatorStrategy> Strategies { get; }
}
