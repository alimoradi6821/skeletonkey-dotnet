using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Specification;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers root workflow and specification validation.
/// </summary>
public sealed class RootValidationTests
{
    /// <summary>
    /// Verifies that the current schema URI is accepted.
    /// </summary>
    [Fact]
    public void AcceptsCurrentSchemaUri()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(schema: WorkflowSpecification.CurrentSchemaUri));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidSchemaUri);
    }

    /// <summary>
    /// Verifies that an incorrect schema URI is rejected.
    /// </summary>
    [Fact]
    public void RejectsIncorrectSchemaUri()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(schema: "https://example.invalid/schema.json"));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidSchemaUri && issue.Path == "/$schema");
    }

    /// <summary>
    /// Verifies that the current specification version is accepted.
    /// </summary>
    [Fact]
    public void AcceptsCurrentSpecificationVersion()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(specVersion: WorkflowSpecification.CurrentVersion));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidSpecificationVersion);
    }

    /// <summary>
    /// Verifies that an incorrect specification version is rejected.
    /// </summary>
    [Fact]
    public void RejectsIncorrectSpecificationVersion()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(specVersion: "9.9.9"));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidSpecificationVersion && issue.Path == "/specVersion");
    }

    /// <summary>
    /// Verifies that an empty workflow ID is rejected.
    /// </summary>
    [Fact]
    public void RejectsEmptyWorkflowId()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(id: ""));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.WorkflowIdRequired && issue.Path == "/id");
    }

    /// <summary>
    /// Verifies that an invalid workflow ID is rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidWorkflowId()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(id: "1-start"));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidWorkflowId && issue.Path == "/id");
    }

    /// <summary>
    /// Verifies that a whitespace workflow name is rejected.
    /// </summary>
    [Fact]
    public void RejectsWhitespaceWorkflowName()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(name: "   "));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.WorkflowNameRequired && issue.Path == "/name");
    }
}
