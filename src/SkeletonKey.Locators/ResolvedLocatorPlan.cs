using System.Collections.ObjectModel;

namespace SkeletonKey.Locators;

/// <summary>
/// Represents a browser-free resolved locator plan with ordered scopes and strategy fallbacks.
/// </summary>
public sealed class ResolvedLocatorPlan
{
    /// <summary>
    /// Initializes a resolved locator plan.
    /// </summary>
    public ResolvedLocatorPlan(
        string catalogId,
        string catalogVersion,
        string locatorId,
        string? description,
        LocatorCardinality cardinality,
        IReadOnlyList<ResolvedLocatorStrategy> strategies,
        IReadOnlyList<ResolvedLocatorScope>? scopes = null,
        string? sourceIdentity = null,
        LocatorResolutionTrace? trace = null)
    {
        CatalogId = catalogId;
        CatalogVersion = catalogVersion;
        LocatorId = locatorId;
        Description = description;
        Cardinality = cardinality;
        Strategies = new ReadOnlyCollection<ResolvedLocatorStrategy>([.. strategies]);
        Scopes = scopes is null ? Array.AsReadOnly(Array.Empty<ResolvedLocatorScope>()) : new ReadOnlyCollection<ResolvedLocatorScope>([.. scopes]);
        SourceIdentity = sourceIdentity ?? $"{catalogId}@{catalogVersion}#{locatorId}";
        Trace = trace ?? new LocatorResolutionTrace();
    }

    /// <summary>Gets the exact locator catalog ID.</summary>
    public string CatalogId { get; }

    /// <summary>Gets the exact locator catalog version.</summary>
    public string CatalogVersion { get; }

    /// <summary>Gets the local locator ID.</summary>
    public string LocatorId { get; }

    /// <summary>Gets optional locator description text.</summary>
    public string? Description { get; }

    /// <summary>Gets the declared locator cardinality.</summary>
    public LocatorCardinality Cardinality { get; }

    /// <summary>Gets the ordered strategy fallbacks.</summary>
    public IReadOnlyList<ResolvedLocatorStrategy> Strategies { get; }

    /// <summary>Gets the outer-to-inner scope chain.</summary>
    public IReadOnlyList<ResolvedLocatorScope> Scopes { get; }

    /// <summary>Gets a stable source identity for diagnostics.</summary>
    public string SourceIdentity { get; }

    /// <summary>Gets immutable resolution diagnostic trace metadata.</summary>
    public LocatorResolutionTrace Trace { get; }
}
