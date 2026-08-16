using SkeletonKey.Validation.Tests.Support;

namespace SkeletonKey.Validation.Tests.Diagnostics;

/// <summary>
/// Covers semantic validation result model behavior.
/// </summary>
public sealed class WorkflowValidationResultTests
{
    /// <summary>
    /// Verifies that an empty result is valid.
    /// </summary>
    [Fact]
    public void ValidResultHasIsValidTrue()
    {
        WorkflowValidationResult result = new();

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    /// <summary>
    /// Verifies that error issues make a result invalid.
    /// </summary>
    [Fact]
    public void ErrorIssueMakesIsValidFalse()
    {
        WorkflowValidationResult result = new([ValidationTestData.Issue(WorkflowValidationCodes.WorkflowIdRequired)]);

        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Verifies that warning issues do not make a result invalid.
    /// </summary>
    [Fact]
    public void WarningIssueDoesNotMakeIsValidFalse()
    {
        WorkflowValidationResult result = new([ValidationTestData.Issue(WorkflowValidationCodes.UnreachableNode, WorkflowValidationSeverity.Warning)]);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that error and warning collections are filtered by severity.
    /// </summary>
    [Fact]
    public void ErrorsAndWarningsCollectionsFilterBySeverity()
    {
        WorkflowValidationIssue error = ValidationTestData.Issue(WorkflowValidationCodes.WorkflowIdRequired);
        WorkflowValidationIssue warning = ValidationTestData.Issue(WorkflowValidationCodes.UnreachableNode, WorkflowValidationSeverity.Warning);

        WorkflowValidationResult result = new([error, warning]);

        Assert.Equal([error], result.Errors);
        Assert.Equal([warning], result.Warnings);
    }

    /// <summary>
    /// Verifies that issue collections cannot be mutated through the result API.
    /// </summary>
    [Fact]
    public void IssueCollectionsAreImmutable()
    {
        WorkflowValidationResult result = new([ValidationTestData.Issue(WorkflowValidationCodes.WorkflowIdRequired)]);

        Assert.Throws<NotSupportedException>(() => ((ICollection<WorkflowValidationIssue>)result.Issues).Add(ValidationTestData.Issue(WorkflowValidationCodes.WorkflowNameRequired)));
        Assert.Throws<NotSupportedException>(() => ((ICollection<WorkflowValidationIssue>)result.Errors).Clear());
    }
}
