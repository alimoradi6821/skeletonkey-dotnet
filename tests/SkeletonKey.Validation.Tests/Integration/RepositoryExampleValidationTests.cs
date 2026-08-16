using SkeletonKey.Serialization.Json;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Validation.Tests.Integration;

/// <summary>
/// Covers semantic validation integration with repository workflow JSON examples.
/// </summary>
public sealed class RepositoryExampleValidationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();
    private readonly WorkflowSemanticValidator _validator = new();

    /// <summary>
    /// Verifies that the repository minimal example is semantically valid.
    /// </summary>
    [Fact]
    public async Task RepositoryMinimalExampleIsSemanticallyValid()
    {
        WorkflowDocument workflow = await ReadWorkflowAsync("examples", "minimal.workflow.json");

        WorkflowValidationResult result = _validator.Validate(workflow);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that the repository minimal example produces no warnings.
    /// </summary>
    [Fact]
    public async Task RepositoryMinimalExampleProducesNoWarnings()
    {
        WorkflowDocument workflow = await ReadWorkflowAsync("examples", "minimal.workflow.json");

        WorkflowValidationResult result = _validator.Validate(workflow);

        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// Verifies that the valid validation fixture is semantically valid.
    /// </summary>
    [Fact]
    public async Task ValidFixtureIsSemanticallyValid()
    {
        WorkflowDocument workflow = await ReadWorkflowAsync("tests", "fixtures", "validation", "valid-minimal.workflow.json");

        WorkflowValidationResult result = _validator.Validate(workflow);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that the duplicate-node fixture reports a duplicate node issue.
    /// </summary>
    [Fact]
    public async Task DuplicateNodeFixtureReportsDuplicateNode()
    {
        WorkflowDocument workflow = await ReadWorkflowAsync("tests", "fixtures", "validation", "invalid-duplicate-node.workflow.json");

        WorkflowValidationResult result = _validator.Validate(workflow);

        Assert.Contains(result.Errors, static issue => issue.Code == WorkflowValidationCodes.DuplicateNodeId);
    }

    /// <summary>
    /// Verifies that the unknown-connection-node fixture reports an unknown target node.
    /// </summary>
    [Fact]
    public async Task UnknownConnectionNodeFixtureReportsUnknownTargetNode()
    {
        WorkflowDocument workflow = await ReadWorkflowAsync("tests", "fixtures", "validation", "invalid-unknown-connection-node.workflow.json");

        WorkflowValidationResult result = _validator.Validate(workflow);

        Assert.Contains(result.Errors, static issue => issue.Code == WorkflowValidationCodes.UnknownTargetNode);
    }

    /// <summary>
    /// Verifies that the unreachable-node fixture reports an unreachable warning.
    /// </summary>
    [Fact]
    public async Task UnreachableNodeFixtureReportsWarning()
    {
        WorkflowDocument workflow = await ReadWorkflowAsync("tests", "fixtures", "validation", "warning-unreachable-node.workflow.json");

        WorkflowValidationResult result = _validator.Validate(workflow);

        Assert.Contains(result.Warnings, static issue => issue.Code == WorkflowValidationCodes.UnreachableNode);
    }

    private async Task<WorkflowDocument> ReadWorkflowAsync(params string[] pathParts)
    {
        string path = Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "..", .. pathParts]));
        string json = await File.ReadAllTextAsync(path);
        return _serializer.Deserialize(json);
    }
}
