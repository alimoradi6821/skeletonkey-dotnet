using SkeletonKey.Locators;

namespace SkeletonKey.Locators.Runtime;

/// <summary>
/// Represents an exact locator document repository lookup result.
/// </summary>
public sealed class LocatorDocumentLookupResult
{
    /// <summary>
    /// Initializes a locator document lookup result.
    /// </summary>
    /// <param name="found">Whether an exact catalog ID and version matched.</param>
    /// <param name="document">The matched document, when found.</param>
    /// <param name="diagnostic">Optional host-neutral diagnostic text.</param>
    public LocatorDocumentLookupResult(bool found, LocatorDocument? document = null, string? diagnostic = null)
    {
        Found = found;
        Document = document;
        Diagnostic = diagnostic;
    }

    /// <summary>Gets a value indicating whether the exact lookup matched.</summary>
    public bool Found { get; }

    /// <summary>Gets the matched immutable locator document, when found.</summary>
    public LocatorDocument? Document { get; }

    /// <summary>Gets optional host-neutral diagnostic text.</summary>
    public string? Diagnostic { get; }

    /// <summary>Creates a successful exact lookup result.</summary>
    /// <param name="document">The matched immutable locator document.</param>
    /// <returns>The successful lookup result.</returns>
    public static LocatorDocumentLookupResult Success(LocatorDocument document)
    {
        return new(true, document ?? throw new ArgumentNullException(nameof(document)));
    }

    /// <summary>Creates a missing exact lookup result.</summary>
    /// <param name="diagnostic">Optional host-neutral diagnostic text.</param>
    /// <returns>The missing lookup result.</returns>
    public static LocatorDocumentLookupResult Missing(string? diagnostic = null)
    {
        return new(false, diagnostic: diagnostic);
    }
}
