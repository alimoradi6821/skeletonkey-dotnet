using SkeletonKey.Serialization.Json;
using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Validation.Tests.Behavior;

/// <summary>
/// Covers general semantic validator behavior.
/// </summary>
public sealed class WorkflowSemanticValidatorBehaviorTests
{
    /// <summary>
    /// Verifies that validating a null workflow throws.
    /// </summary>
    [Fact]
    public void ValidateNullThrowsArgumentNullException()
    {
        WorkflowSemanticValidator validator = new();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    /// <summary>
    /// Verifies that validation does not mutate workflow documents.
    /// </summary>
    [Fact]
    public void ValidationDoesNotMutateWorkflow()
    {
        WorkflowJsonSerializer serializer = new();
        WorkflowDocument workflow = ValidationTestData.CreateValidWorkflow();
        string before = serializer.Serialize(workflow);

        _ = ValidationTestData.Validate(workflow);

        string after = serializer.Serialize(workflow);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Verifies that validation results are deterministic.
    /// </summary>
    [Fact]
    public void ValidationResultIsDeterministic()
    {
        WorkflowDocument workflow = CreateWorkflowWithSeveralIssues();

        string[] first = [.. ValidationTestData.Validate(workflow).Issues.Select(static issue => $"{issue.Code}:{issue.Path}")];
        string[] second = [.. ValidationTestData.Validate(workflow).Issues.Select(static issue => $"{issue.Code}:{issue.Path}")];

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Verifies that a single validator instance supports concurrent validation.
    /// </summary>
    [Fact]
    public async Task SingleValidatorSupportsConcurrentValidation()
    {
        WorkflowSemanticValidator validator = new();
        WorkflowDocument workflow = CreateWorkflowWithSeveralIssues();

        WorkflowValidationResult[] results = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(_ => Task.Run(() => validator.Validate(workflow))));

        string[] expected = [.. results[0].Issues.Select(static issue => $"{issue.Code}:{issue.Path}")];
        Assert.All(results, result =>
        {
            string[] actual = [.. result.Issues.Select(static issue => $"{issue.Code}:{issue.Path}")];
            Assert.Equal(expected, actual);
        });
    }

    /// <summary>
    /// Verifies that repeated validation returns equivalent issue sequences.
    /// </summary>
    [Fact]
    public void RepeatedValidationReturnsEquivalentIssueSequences()
    {
        WorkflowDocument workflow = CreateWorkflowWithSeveralIssues();

        WorkflowValidationIssue[] first = [.. ValidationTestData.Validate(workflow).Issues];
        WorkflowValidationIssue[] second = [.. ValidationTestData.Validate(workflow).Issues];

        Assert.Equal(
            first.Select(static issue => (issue.Code, issue.Severity, issue.Path)),
            second.Select(static issue => (issue.Code, issue.Severity, issue.Path)));
    }

    private static WorkflowDocument CreateWorkflowWithSeveralIssues()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            new("start", "core.log", 0),
            ValidationTestData.Node("unreachable"),
        ];
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "bad port"), new WorkflowEndpoint("missing", "main")),
        ];

        return ValidationTestData.CreateValidWorkflow(id: "1-invalid", name: " ", nodes: nodes, connections: connections);
    }
}
