using SkeletonKey.Conformance.Tests.Support;
using SkeletonKey.Serialization.Json;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers conformance fixture behavior across strict serialization, JSON Schema, and semantic validation.
/// </summary>
public sealed class ConformanceFixtureTests
{
    private readonly ConformanceManifest _manifest = ConformanceManifest.Load();
    private readonly JsonSchemaConformanceValidator _schemaValidator = new(RepositoryPaths.SchemaPath);
    private readonly WorkflowJsonSerializer _serializer = new();
    private readonly WorkflowSemanticValidator _semanticValidator = new();

    /// <summary>
    /// Verifies that every valid fixture passes all applicable layers.
    /// </summary>
    [Fact]
    public void EveryValidFixturePassesAllLayers()
    {
        foreach (ConformanceCase testCase in Cases("valid"))
        {
            WorkflowDocument workflow = AssertDeserializes(testCase);
            AssertSchemaExpectation(testCase);
            AssertSemanticExpectation(testCase, workflow);
        }
    }

    /// <summary>
    /// Verifies that every serialization-invalid fixture fails strict deserialization.
    /// </summary>
    [Fact]
    public void EverySerializationInvalidFixtureFailsStrictDeserialization()
    {
        foreach (ConformanceCase testCase in Cases("serialization-invalid"))
        {
            Assert.Equal("failure", testCase.Serialization);
            Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(testCase.ReadJson()));
            AssertSchemaExpectation(testCase);
            Assert.Null(testCase.Semantic);
        }
    }

    /// <summary>
    /// Verifies that every schema-invalid fixture fails JSON Schema validation.
    /// </summary>
    [Fact]
    public void EverySchemaInvalidFixtureFailsJsonSchemaValidation()
    {
        foreach (ConformanceCase testCase in Cases("schema-invalid"))
        {
            Assert.Equal("invalid", testCase.Schema);
            SchemaValidationResult schemaResult = _schemaValidator.Validate(testCase.ReadJson());
            Assert.True(schemaResult.IsApplicable);
            Assert.False(schemaResult.IsValid);
            AssertSerializationExpectation(testCase);
        }
    }

    /// <summary>
    /// Verifies that every semantic-invalid fixture passes parsing and schema validation but fails semantic validation.
    /// </summary>
    [Fact]
    public void EverySemanticInvalidFixtureFailsSemanticValidationOnly()
    {
        foreach (ConformanceCase testCase in Cases("semantic-invalid"))
        {
            WorkflowDocument workflow = AssertDeserializes(testCase);
            AssertSchemaExpectation(testCase);

            WorkflowValidationResult result = _semanticValidator.Validate(workflow);

            Assert.False(result.IsValid);
            AssertSemanticExpectation(testCase, workflow);
        }
    }

    /// <summary>
    /// Verifies that every warning fixture remains valid but reports expected warnings.
    /// </summary>
    [Fact]
    public void EveryWarningFixtureReportsExpectedWarningsOnly()
    {
        foreach (ConformanceCase testCase in Cases("warning"))
        {
            WorkflowDocument workflow = AssertDeserializes(testCase);
            AssertSchemaExpectation(testCase);

            WorkflowValidationResult result = _semanticValidator.Validate(workflow);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
            AssertSemanticExpectation(testCase, workflow);
        }
    }

    private IEnumerable<ConformanceCase> Cases(string category)
    {
        return _manifest.Cases.Where(testCase => string.Equals(testCase.Category, category, StringComparison.Ordinal));
    }

    private WorkflowDocument AssertDeserializes(ConformanceCase testCase)
    {
        Assert.Equal("success", testCase.Serialization);
        return _serializer.Deserialize(testCase.ReadJson());
    }

    private void AssertSerializationExpectation(ConformanceCase testCase)
    {
        if (string.Equals(testCase.Serialization, "success", StringComparison.Ordinal))
        {
            _ = _serializer.Deserialize(testCase.ReadJson());
        }
        else
        {
            Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(testCase.ReadJson()));
        }
    }

    private void AssertSchemaExpectation(ConformanceCase testCase)
    {
        SchemaValidationResult result = _schemaValidator.Validate(testCase.ReadJson());

        switch (testCase.Schema)
        {
            case "valid":
                Assert.True(result.IsApplicable);
                Assert.True(result.IsValid);
                break;
            case "invalid":
                Assert.True(result.IsApplicable);
                Assert.False(result.IsValid);
                break;
            case "not-applicable":
                Assert.False(result.IsApplicable);
                break;
            default:
                throw new InvalidOperationException($"Unknown schema expectation '{testCase.Schema}'.");
        }
    }

    private void AssertSemanticExpectation(ConformanceCase testCase, WorkflowDocument workflow)
    {
        Assert.NotNull(testCase.Semantic);
        WorkflowValidationResult result = _semanticValidator.Validate(workflow);

        Assert.Equal(testCase.Semantic.IsValid, result.IsValid);
        Assert.Equal(testCase.Semantic.Errors, [.. result.Errors.Select(static issue => issue.Code)]);
        Assert.Equal(testCase.Semantic.Warnings, [.. result.Warnings.Select(static issue => issue.Code)]);
    }
}
