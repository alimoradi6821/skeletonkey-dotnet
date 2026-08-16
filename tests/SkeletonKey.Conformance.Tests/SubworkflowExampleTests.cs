using SkeletonKey.Conformance.Tests.Support;
using SkeletonKey.Serialization.Json;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers repository subworkflow examples across all current validation layers.
/// </summary>
public sealed class SubworkflowExampleTests
{
    private readonly JsonSchemaConformanceValidator _schemaValidator = new(RepositoryPaths.SchemaPath);
    private readonly WorkflowJsonSerializer _serializer = new();
    private readonly WorkflowSemanticValidator _semanticValidator = new();

    /// <summary>
    /// Verifies subworkflow examples deserialize, validate, and round-trip canonically.
    /// </summary>
    [Theory]
    [InlineData("check-account.workflow.json")]
    [InlineData("send-message.workflow.json")]
    public void SubworkflowExamplesPassAllCurrentValidationLayers(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "examples", "subworkflows", fileName));
        string json = File.ReadAllText(path);

        WorkflowDocument workflow = _serializer.Deserialize(json);
        WorkflowValidationResult semanticResult = _semanticValidator.Validate(workflow);
        SchemaValidationResult schemaResult = _schemaValidator.Validate(json);

        Assert.True(schemaResult.IsApplicable);
        Assert.True(schemaResult.IsValid);
        Assert.True(semanticResult.IsValid);
        Assert.Empty(semanticResult.Warnings);
        Assert.Equal(_serializer.Serialize(workflow), json);
    }
}
