using SkeletonKey.Locators;

namespace SkeletonKey.Locators.Runtime;

/// <summary>
/// Looks up immutable locator catalog documents by exact case-sensitive identity.
/// </summary>
public interface ILocatorDocumentRepository
{
    /// <summary>
    /// Gets a locator document by exact catalog ID and exact version.
    /// </summary>
    /// <param name="catalogId">The case-sensitive locator catalog ID.</param>
    /// <param name="version">The exact locator catalog version.</param>
    /// <param name="cancellationToken">A token used to cancel repository lookup.</param>
    /// <returns>A lookup result containing the immutable document when found.</returns>
    public ValueTask<LocatorDocumentLookupResult> GetAsync(
        string catalogId,
        string version,
        CancellationToken cancellationToken = default);
}
