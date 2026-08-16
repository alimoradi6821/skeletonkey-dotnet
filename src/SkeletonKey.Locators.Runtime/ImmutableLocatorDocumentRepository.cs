using System.Collections.ObjectModel;
using SkeletonKey.Locators;

namespace SkeletonKey.Locators.Runtime;

/// <summary>
/// Provides a thread-safe immutable in-memory locator document repository.
/// </summary>
public sealed class ImmutableLocatorDocumentRepository : ILocatorDocumentRepository
{
    private readonly IReadOnlyDictionary<LocatorDocumentKey, LocatorDocument> _documents;

    /// <summary>
    /// Initializes the repository from immutable locator documents.
    /// </summary>
    /// <param name="documents">Documents keyed by their exact ID and version.</param>
    /// <exception cref="ArgumentException">Thrown when duplicate exact catalog identities are supplied.</exception>
    public ImmutableLocatorDocumentRepository(IEnumerable<LocatorDocument>? documents = null)
    {
        Dictionary<LocatorDocumentKey, LocatorDocument> copy = new();
        foreach (LocatorDocument document in documents ?? Array.Empty<LocatorDocument>())
        {
            LocatorDocumentKey key = new(document.Id, document.SpecVersion);
            if (!copy.TryAdd(key, document))
            {
                throw new ArgumentException("Duplicate exact locator catalog identities are not allowed.", nameof(documents));
            }
        }

        _documents = new ReadOnlyDictionary<LocatorDocumentKey, LocatorDocument>(copy);
    }

    /// <inheritdoc />
    public ValueTask<LocatorDocumentLookupResult> GetAsync(string catalogId, string version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_documents.TryGetValue(new LocatorDocumentKey(catalogId, version), out LocatorDocument? document)
            ? LocatorDocumentLookupResult.Success(document)
            : LocatorDocumentLookupResult.Missing("The exact locator catalog ID and version were not found."));
    }

    private readonly record struct LocatorDocumentKey(string CatalogId, string Version);
}
