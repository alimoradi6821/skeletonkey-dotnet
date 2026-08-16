using System.Collections.ObjectModel;

namespace SkeletonKey.Catalog.Validation;

/// <summary>
/// Contains deterministic semantic validation diagnostics for a node catalog document.
/// </summary>
public sealed class NodeCatalogValidationResult
{
    /// <summary>
    /// Initializes a validation result.
    /// </summary>
    /// <param name="issues">The ordered validation issues.</param>
    public NodeCatalogValidationResult(IReadOnlyList<NodeCatalogValidationIssue>? issues = null)
    {
        Issues = new ReadOnlyCollection<NodeCatalogValidationIssue>(issues is null ? [] : [.. issues]);
    }

    /// <summary>
    /// Gets a value indicating whether no validation issues were reported.
    /// </summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>
    /// Gets ordered validation issues.
    /// </summary>
    public IReadOnlyList<NodeCatalogValidationIssue> Issues { get; }
}
