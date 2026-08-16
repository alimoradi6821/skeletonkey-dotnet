using SkeletonKey.Conformance.Tests.Support;
using SkeletonKey.Serialization.Json;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers the repository canonical workflow example across validation layers.
/// </summary>
public sealed class CanonicalExampleTests
{
    private readonly JsonSchemaConformanceValidator _schemaValidator = new(RepositoryPaths.SchemaPath);
    private readonly WorkflowJsonSerializer _serializer = new();
    private readonly WorkflowSemanticValidator _semanticValidator = new();

    /// <summary>
    /// Verifies that the repository minimal example passes the normative JSON Schema.
    /// </summary>
    [Fact]
    public void RepositoryMinimalExamplePassesNormativeJsonSchema()
    {
        SchemaValidationResult result = _schemaValidator.Validate(File.ReadAllText(RepositoryPaths.ExamplePath));

        Assert.True(result.IsApplicable);
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that the repository minimal example deserializes successfully.
    /// </summary>
    [Fact]
    public void RepositoryMinimalExampleDeserializesSuccessfully()
    {
        WorkflowDocument workflow = _serializer.Deserialize(File.ReadAllText(RepositoryPaths.ExamplePath));

        Assert.Equal("minimal-workflow", workflow.Id);
    }

    /// <summary>
    /// Verifies that the repository minimal example passes semantic validation.
    /// </summary>
    [Fact]
    public void RepositoryMinimalExamplePassesSemanticValidation()
    {
        WorkflowDocument workflow = _serializer.Deserialize(File.ReadAllText(RepositoryPaths.ExamplePath));
        WorkflowValidationResult result = _semanticValidator.Validate(workflow);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that the repository minimal example has no semantic warnings.
    /// </summary>
    [Fact]
    public void RepositoryMinimalExampleHasNoSemanticWarnings()
    {
        WorkflowDocument workflow = _serializer.Deserialize(File.ReadAllText(RepositoryPaths.ExamplePath));
        WorkflowValidationResult result = _semanticValidator.Validate(workflow);

        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// Verifies that canonical serializer output passes the normative JSON Schema.
    /// </summary>
    [Fact]
    public void CanonicalSerializerOutputPassesNormativeJsonSchema()
    {
        WorkflowDocument workflow = _serializer.Deserialize(File.ReadAllText(RepositoryPaths.ExamplePath));
        string canonicalJson = _serializer.Serialize(workflow);

        SchemaValidationResult result = _schemaValidator.Validate(canonicalJson);

        Assert.True(result.IsApplicable);
        Assert.True(result.IsValid);
    }
}
