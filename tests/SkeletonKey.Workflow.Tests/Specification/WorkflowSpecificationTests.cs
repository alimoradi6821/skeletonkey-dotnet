using SkeletonKey.Workflow.Specification;

namespace SkeletonKey.Workflow.Tests.Specification;

/// <summary>
/// Covers workflow language specification constants.
/// </summary>
public sealed class WorkflowSpecificationTests
{
    /// <summary>
    /// Verifies the current workflow language version.
    /// </summary>
    [Fact]
    public void CurrentVersionIsZeroOneZero()
    {
        Assert.Equal("0.1.0", WorkflowSpecification.CurrentVersion);
    }

    /// <summary>
    /// Verifies the current workflow schema URI.
    /// </summary>
    [Fact]
    public void CurrentSchemaUriIsCorrect()
    {
        Assert.Equal("https://schemas.skeletonkey.dev/workflow/0.1/schema.json", WorkflowSpecification.CurrentSchemaUri);
    }
}
