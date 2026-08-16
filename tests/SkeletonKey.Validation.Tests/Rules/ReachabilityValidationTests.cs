using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers basic graph reachability validation.
/// </summary>
public sealed class ReachabilityValidationTests
{
    /// <summary>
    /// Verifies that reachable nodes do not produce reachability warnings.
    /// </summary>
    [Fact]
    public void DoesNotWarnForReachableNodes()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow());

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.UnreachableNode);
    }

    /// <summary>
    /// Verifies that unreachable enabled nodes produce warnings.
    /// </summary>
    [Fact]
    public void WarnsForUnreachableEnabledNode()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            ValidationTestData.Node("reachable"),
            ValidationTestData.Node("unreachable"),
        ];
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("reachable", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: connections));

        Assert.Contains(result.Warnings, static issue => issue.Code == WorkflowValidationCodes.UnreachableNode && issue.Path == "/nodes/2");
    }

    /// <summary>
    /// Verifies that unreachable disabled nodes do not produce reachability warnings.
    /// </summary>
    [Fact]
    public void DoesNotWarnForUnreachableDisabledNode()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            ValidationTestData.Node("disabled", disabled: true),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.UnreachableNode);
    }

    /// <summary>
    /// Verifies that unsafe reachability analysis is skipped when the start node is invalid.
    /// </summary>
    [Fact]
    public void DoesNotRunUnsafeReachabilityAnalysisWhenStartNodeIsInvalid()
    {
        WorkflowNode[] nodes =
        [
            new("1-start", "core.start", 1),
            ValidationTestData.Node("unreachable"),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.UnreachableNode);
    }

    /// <summary>
    /// Verifies that cyclic graphs do not hang reachability analysis.
    /// </summary>
    [Fact]
    public void HandlesCyclicGraphWithoutHanging()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            ValidationTestData.Node("a"),
            ValidationTestData.Node("b"),
        ];
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("a", "main")),
            new(new WorkflowEndpoint("a", "main"), new WorkflowEndpoint("b", "main")),
            new(new WorkflowEndpoint("b", "main"), new WorkflowEndpoint("a", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: connections));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.UnreachableNode);
    }

    /// <summary>
    /// Verifies that unreachable issue ordering follows node order.
    /// </summary>
    [Fact]
    public void ProducesDeterministicUnreachableIssueOrdering()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            ValidationTestData.Node("b"),
            ValidationTestData.Node("a"),
        ];
        WorkflowDocument workflow = ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []);

        string[] paths = [.. ValidationTestData.Validate(workflow).Warnings.Select(static issue => issue.Path)];

        Assert.Equal(["/nodes/1", "/nodes/2"], paths);
    }
}
