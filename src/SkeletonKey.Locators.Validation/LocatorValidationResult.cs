using System.Collections.ObjectModel;

namespace SkeletonKey.Locators.Validation;

/// <summary>
/// Contains deterministic semantic validation diagnostics for a locator document.
/// </summary>
public sealed class LocatorValidationResult
{
    /// <summary>
    /// Initializes a validation result.
    /// </summary>
    /// <param name="issues">The ordered validation issues.</param>
    public LocatorValidationResult(IReadOnlyList<LocatorValidationIssue>? issues = null)
    {
        Issues = new ReadOnlyCollection<LocatorValidationIssue>(issues is null ? [] : [.. issues]);
    }

    /// <summary>Gets a value indicating whether no validation issues were reported.</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>Gets ordered validation issues.</summary>
    public IReadOnlyList<LocatorValidationIssue> Issues { get; }
}
