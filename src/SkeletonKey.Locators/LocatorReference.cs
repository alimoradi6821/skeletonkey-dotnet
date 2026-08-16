namespace SkeletonKey.Locators;

/// <summary>
/// Identifies a locator in a versioned catalog without resolving the catalog or selector.
/// </summary>
public sealed class LocatorReference
{
    /// <summary>
    /// Initializes a locator reference.
    /// </summary>
    /// <param name="catalog">The locator catalog ID.</param>
    /// <param name="id">The local locator ID.</param>
    /// <param name="version">Optional exact Semantic Version 2.0 catalog version.</param>
    public LocatorReference(string catalog, string id, string? version = null)
    {
        Catalog = catalog;
        Version = version;
        Id = id;
    }

    /// <summary>Gets the locator catalog ID.</summary>
    public string Catalog { get; }

    /// <summary>Gets the optional exact catalog version.</summary>
    public string? Version { get; }

    /// <summary>Gets the local locator ID.</summary>
    public string Id { get; }
}
