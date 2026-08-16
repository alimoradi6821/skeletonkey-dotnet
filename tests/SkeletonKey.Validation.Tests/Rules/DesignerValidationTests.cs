using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Designer;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers designer metadata validation.
/// </summary>
public sealed class DesignerValidationTests
{
    /// <summary>
    /// Verifies that positions for existing nodes are accepted.
    /// </summary>
    [Fact]
    public void AcceptsPositionForExistingNode()
    {
        WorkflowDesignerMetadata designer = new(
            positions: new Dictionary<string, WorkflowNodePosition>
            {
                ["start"] = new(0, 0),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.DesignerPositionUnknownNode);
    }

    /// <summary>
    /// Verifies that positions for unknown nodes produce warnings.
    /// </summary>
    [Fact]
    public void WarnsWhenPositionReferencesUnknownNode()
    {
        WorkflowDesignerMetadata designer = new(
            positions: new Dictionary<string, WorkflowNodePosition>
            {
                ["missing"] = new(0, 0),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.Contains(result.Warnings, static issue => issue.Code == WorkflowValidationCodes.DesignerPositionUnknownNode && issue.Path == "/designer/positions/missing");
    }

    /// <summary>
    /// Verifies that sizes for unknown nodes produce warnings.
    /// </summary>
    [Fact]
    public void WarnsWhenSizeReferencesUnknownNode()
    {
        WorkflowDesignerMetadata designer = new(
            sizes: new Dictionary<string, WorkflowNodeSize>
            {
                ["missing"] = new(100, 50),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.Contains(result.Warnings, static issue => issue.Code == WorkflowValidationCodes.DesignerSizeUnknownNode && issue.Path == "/designer/sizes/missing");
    }

    /// <summary>
    /// Verifies that non-finite X positions produce warnings.
    /// </summary>
    [Fact]
    public void WarnsForNonFiniteX()
    {
        WorkflowDesignerMetadata designer = new(
            positions: new Dictionary<string, WorkflowNodePosition>
            {
                ["start"] = new(double.NaN, 0),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.Contains(result.Warnings, static issue => issue.Code == WorkflowValidationCodes.InvalidDesignerPosition && issue.Path == "/designer/positions/start/x");
    }

    /// <summary>
    /// Verifies that non-finite Y positions produce warnings.
    /// </summary>
    [Fact]
    public void WarnsForNonFiniteY()
    {
        WorkflowDesignerMetadata designer = new(
            positions: new Dictionary<string, WorkflowNodePosition>
            {
                ["start"] = new(0, double.PositiveInfinity),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.Contains(result.Warnings, static issue => issue.Code == WorkflowValidationCodes.InvalidDesignerPosition && issue.Path == "/designer/positions/start/y");
    }

    /// <summary>
    /// Verifies that non-positive widths produce warnings.
    /// </summary>
    [Fact]
    public void WarnsForNonPositiveWidth()
    {
        WorkflowDesignerMetadata designer = new(
            sizes: new Dictionary<string, WorkflowNodeSize>
            {
                ["start"] = new(0, 50),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.Contains(result.Warnings, static issue => issue.Code == WorkflowValidationCodes.InvalidDesignerSize && issue.Path == "/designer/sizes/start/width");
    }

    /// <summary>
    /// Verifies that non-positive heights produce warnings.
    /// </summary>
    [Fact]
    public void WarnsForNonPositiveHeight()
    {
        WorkflowDesignerMetadata designer = new(
            sizes: new Dictionary<string, WorkflowNodeSize>
            {
                ["start"] = new(50, -1),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.Contains(result.Warnings, static issue => issue.Code == WorkflowValidationCodes.InvalidDesignerSize && issue.Path == "/designer/sizes/start/height");
    }

    /// <summary>
    /// Verifies that designer warnings do not invalidate workflows.
    /// </summary>
    [Fact]
    public void DesignerWarningsDoNotInvalidateWorkflow()
    {
        WorkflowDesignerMetadata designer = new(
            positions: new Dictionary<string, WorkflowNodePosition>
            {
                ["missing"] = new(0, 0),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
    }

    /// <summary>
    /// Verifies that designer node IDs are escaped in JSON Pointer paths.
    /// </summary>
    [Fact]
    public void EscapesDesignerNodeIdsInJsonPointerPaths()
    {
        WorkflowDesignerMetadata designer = new(
            positions: new Dictionary<string, WorkflowNodePosition>
            {
                ["bad/node"] = new(0, 0),
            });

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(designer: designer));

        Assert.Contains(result.Warnings, static issue => issue.Path == "/designer/positions/bad~1node");
    }
}
