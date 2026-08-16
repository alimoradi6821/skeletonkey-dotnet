using System.Collections.ObjectModel;

namespace SkeletonKey.Validation;

/// <summary>
/// Represents an immutable semantic validation result.
/// </summary>
/// <remarks>
/// A result is valid only when it contains no error issues. Warning issues report non-fatal concerns and
/// do not make the workflow invalid. Collections are never <see langword="null" /> and cannot be mutated
/// through this API.
/// </remarks>
public sealed class WorkflowValidationResult
{
    private static readonly IReadOnlyList<WorkflowValidationIssue> _emptyIssues = Array.AsReadOnly(Array.Empty<WorkflowValidationIssue>());

    /// <summary>
    /// Initializes a new semantic validation result.
    /// </summary>
    /// <param name="issues">The validation issues in deterministic order.</param>
    public WorkflowValidationResult(IEnumerable<WorkflowValidationIssue>? issues = null)
    {
        Issues = issues is null
            ? _emptyIssues
            : Array.AsReadOnly([.. issues]);

        Errors = new ReadOnlyCollection<WorkflowValidationIssue>(
            [.. Issues.Where(static issue => issue.Severity == WorkflowValidationSeverity.Error)]);
        Warnings = new ReadOnlyCollection<WorkflowValidationIssue>(
            [.. Issues.Where(static issue => issue.Severity == WorkflowValidationSeverity.Warning)]);
        IsValid = Errors.Count == 0;
    }

    /// <summary>
    /// Gets all validation issues in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowValidationIssue> Issues { get; }

    /// <summary>
    /// Gets a value indicating whether the workflow has no error issues.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets only error issues. Errors make the workflow invalid.
    /// </summary>
    public IReadOnlyList<WorkflowValidationIssue> Errors { get; }

    /// <summary>
    /// Gets only warning issues. Warnings do not make the workflow invalid.
    /// </summary>
    public IReadOnlyList<WorkflowValidationIssue> Warnings { get; }
}
