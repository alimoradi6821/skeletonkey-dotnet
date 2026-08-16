using SkeletonKey.Conformance.Tests.Support;
using SkeletonKey.Serialization.Json;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers behavior intentionally split across validation layers.
/// </summary>
public sealed class CrossLayerBehaviorTests
{
    private readonly ConformanceManifest _manifest = ConformanceManifest.Load();
    private readonly JsonSchemaConformanceValidator _schemaValidator = new(RepositoryPaths.SchemaPath);
    private readonly WorkflowJsonSerializer _serializer = new();
    private readonly WorkflowSemanticValidator _semanticValidator = new();

    /// <summary>
    /// Verifies that schema validation does not replace duplicate-property detection.
    /// </summary>
    [Fact]
    public void SchemaValidationDoesNotReplaceDuplicatePropertyDetection()
    {
        ConformanceCase testCase = Case("serialization-invalid-duplicate-nested-parameter");

        SchemaValidationResult schemaResult = _schemaValidator.Validate(testCase.ReadJson());

        Assert.True(schemaResult.IsApplicable);
        Assert.True(schemaResult.IsValid);
        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(testCase.ReadJson()));
    }

    /// <summary>
    /// Verifies that schema validation does not enforce unique node IDs.
    /// </summary>
    [Fact]
    public void SchemaValidationDoesNotEnforceUniqueNodeIds()
    {
        AssertSchemaValidButSemanticCode("semantic-invalid-duplicate-node-id", WorkflowValidationCodes.DuplicateNodeId);
    }

    /// <summary>
    /// Verifies that schema validation does not enforce node references.
    /// </summary>
    [Fact]
    public void SchemaValidationDoesNotEnforceNodeReferences()
    {
        AssertSchemaValidButSemanticCode("semantic-invalid-unknown-connection-source", WorkflowValidationCodes.UnknownSourceNode);
    }

    /// <summary>
    /// Verifies that schema validation does not enforce graph reachability.
    /// </summary>
    [Fact]
    public void SchemaValidationDoesNotEnforceGraphReachability()
    {
        ConformanceCase testCase = Case("warning-unreachable-node");
        WorkflowDocument workflow = _serializer.Deserialize(testCase.ReadJson());

        Assert.True(_schemaValidator.Validate(testCase.ReadJson()).IsValid);
        Assert.Contains(_semanticValidator.Validate(workflow).Warnings, static issue => issue.Code == WorkflowValidationCodes.UnreachableNode);
    }

    /// <summary>
    /// Verifies that semantic validation does not mutate deserialized workflows.
    /// </summary>
    [Fact]
    public void SemanticValidationDoesNotMutateDeserializedWorkflows()
    {
        ConformanceCase testCase = Case("valid-nested-parameters");
        WorkflowDocument workflow = _serializer.Deserialize(testCase.ReadJson());
        string before = _serializer.Serialize(workflow);

        _ = _semanticValidator.Validate(workflow);

        string after = _serializer.Serialize(workflow);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Verifies that repeated conformance runs produce identical semantic diagnostics.
    /// </summary>
    [Fact]
    public void RepeatedConformanceRunsProduceIdenticalDiagnostics()
    {
        string[] first = [.. RunSemanticDiagnostics()];
        string[] second = [.. RunSemanticDiagnostics()];

        Assert.Equal(first, second);
    }

    private void AssertSchemaValidButSemanticCode(string id, string code)
    {
        ConformanceCase testCase = Case(id);
        WorkflowDocument workflow = _serializer.Deserialize(testCase.ReadJson());

        Assert.True(_schemaValidator.Validate(testCase.ReadJson()).IsValid);
        Assert.Contains(_semanticValidator.Validate(workflow).Issues, issue => issue.Code == code);
    }

    private IEnumerable<string> RunSemanticDiagnostics()
    {
        foreach (ConformanceCase testCase in _manifest.Cases.Where(static item => item.Semantic is not null))
        {
            WorkflowDocument workflow = _serializer.Deserialize(testCase.ReadJson());
            WorkflowValidationResult result = _semanticValidator.Validate(workflow);
            foreach (WorkflowValidationIssue issue in result.Issues)
            {
                yield return $"{testCase.Id}:{issue.Code}:{issue.Path}";
            }
        }
    }

    private ConformanceCase Case(string id)
    {
        return _manifest.Cases.Single(testCase => string.Equals(testCase.Id, id, StringComparison.Ordinal));
    }
}
