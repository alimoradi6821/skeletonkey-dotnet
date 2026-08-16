using SkeletonKey.Conformance.Tests.Support;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Json;
using SkeletonKey.Locators.Validation;
using SkeletonKey.Serialization.Json;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers resource, locator, and interaction repository examples.
/// </summary>
public sealed class ResourceLocatorInteractionExampleTests
{
    private readonly JsonSchemaConformanceValidator _workflowSchemaValidator = new(RepositoryPaths.SchemaPath);
    private readonly WorkflowJsonSerializer _workflowSerializer = new();
    private readonly WorkflowSemanticValidator _workflowValidator = new();
    private readonly LocatorJsonSerializer _locatorSerializer = new();
    private readonly LocatorSemanticValidator _locatorValidator = new();
    private readonly LocatorJsonSchemaConformanceValidator _locatorSchemaValidator = new();

    /// <summary>
    /// Verifies new workflow examples pass all workflow validation layers and round-trip canonically.
    /// </summary>
    [Theory]
    [InlineData("resources", "browser-resource.workflow.json")]
    [InlineData("resources", "subworkflow-resource-mapping.workflow.json")]
    [InlineData("interactions", "manual-login.workflow.json")]
    public void WorkflowExamplesPassAllValidationLayers(string directory, string fileName)
    {
        string path = Path.Combine(RepositoryPaths.Root, "examples", directory, fileName);
        string json = File.ReadAllText(path);
        WorkflowDocument workflow = _workflowSerializer.Deserialize(json);
        WorkflowValidationResult validation = _workflowValidator.Validate(workflow);

        SchemaValidationResult schema = _workflowSchemaValidator.Validate(json);
        string canonical = _workflowSerializer.Serialize(workflow);

        Assert.True(schema.IsValid);
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Warnings);
        Assert.Equal(canonical, json);
    }

    /// <summary>
    /// Verifies the locator example passes all locator validation layers and round-trips canonically.
    /// </summary>
    [Fact]
    public void LocatorExamplePassesAllValidationLayers()
    {
        string path = Path.Combine(RepositoryPaths.Root, "examples", "locators", "bale-contacts.locators.json");
        string json = File.ReadAllText(path);
        LocatorDocument document = _locatorSerializer.Deserialize(json);
        LocatorValidationResult validation = _locatorValidator.Validate(document);
        string canonical = _locatorSerializer.Serialize(document);

        Assert.True(_locatorSchemaValidator.Validate(json));
        Assert.True(validation.IsValid);
        Assert.Equal(canonical, json);
    }
}
