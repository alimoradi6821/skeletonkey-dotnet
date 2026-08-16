using System.Collections.ObjectModel;

namespace SkeletonKey.Locators;

/// <summary>
/// Represents one resolved locator scope in an outer-to-inner scope chain.
/// </summary>
public sealed class ResolvedLocatorScope
{
    /// <summary>
    /// Initializes a resolved locator scope.
    /// </summary>
    public ResolvedLocatorScope(string locatorId, LocatorCardinality cardinality, IReadOnlyList<ResolvedLocatorStrategy> strategies, string? description = null)
    {
        LocatorId = locatorId;
        Cardinality = cardinality;
        Strategies = new ReadOnlyCollection<ResolvedLocatorStrategy>([.. strategies]);
        Description = description;
    }

    /// <summary>Gets the local scoped locator ID.</summary>
    public string LocatorId { get; }

    /// <summary>Gets the scoped locator cardinality.</summary>
    public LocatorCardinality Cardinality { get; }

    /// <summary>Gets ordered scope strategies.</summary>
    public IReadOnlyList<ResolvedLocatorStrategy> Strategies { get; }

    /// <summary>Gets optional scope description text.</summary>
    public string? Description { get; }
}
