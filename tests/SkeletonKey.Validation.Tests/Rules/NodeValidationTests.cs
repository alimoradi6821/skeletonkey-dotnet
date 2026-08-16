using System.Text.Json.Nodes;
using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers workflow node declaration validation.
/// </summary>
public sealed class NodeValidationTests
{
    /// <summary>
    /// Verifies that an empty node list is rejected.
    /// </summary>
    [Fact]
    public void RejectsEmptyNodeList()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: [], connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.WorkflowHasNoNodes && issue.Path == "/nodes");
    }

    /// <summary>
    /// Verifies that exactly one start node is accepted.
    /// </summary>
    [Fact]
    public void AcceptsExactlyOneStartNode()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow());

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidStartNodeCount);
    }

    /// <summary>
    /// Verifies that a missing start node is rejected.
    /// </summary>
    [Fact]
    public void RejectsMissingStartNode()
    {
        WorkflowNode[] nodes = [ValidationTestData.Node("log")];
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidStartNodeCount && issue.Message.Contains("0", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that multiple start nodes are rejected.
    /// </summary>
    [Fact]
    public void RejectsMultipleStartNodes()
    {
        WorkflowNode[] nodes =
        [
            new("start1", "core.start", 1),
            new("start2", "core.start", 1),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidStartNodeCount && issue.Message.Contains("2", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that a disabled start node is rejected.
    /// </summary>
    [Fact]
    public void RejectsDisabledStartNode()
    {
        WorkflowNode[] nodes = [new("start", "core.start", 1, disabled: true)];
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.StartNodeIsDisabled && issue.Path == "/nodes/0/disabled");
    }

    /// <summary>
    /// Verifies that empty node IDs are rejected.
    /// </summary>
    [Fact]
    public void RejectsEmptyNodeId()
    {
        WorkflowNode[] nodes = [new("", "core.start", 1)];
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.NodeIdRequired && issue.Path == "/nodes/0/id");
    }

    /// <summary>
    /// Verifies that invalid node IDs are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidNodeId()
    {
        WorkflowNode[] nodes = [new("node.name", "core.start", 1)];
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidNodeId && issue.Path == "/nodes/0/id");
    }

    /// <summary>
    /// Verifies that duplicate node IDs are rejected.
    /// </summary>
    [Fact]
    public void RejectsDuplicateNodeId()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            ValidationTestData.Node("start"),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.DuplicateNodeId && issue.Path == "/nodes/1/id" && issue.Message.Contains("start", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that node IDs are treated as case-sensitive.
    /// </summary>
    [Fact]
    public void TreatsNodeIdsAsCaseSensitive()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            ValidationTestData.Node("Start"),
        ];
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("Start", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: connections));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.DuplicateNodeId);
    }

    /// <summary>
    /// Verifies that empty node types are rejected.
    /// </summary>
    [Fact]
    public void RejectsEmptyNodeType()
    {
        WorkflowNode[] nodes = [new("start", "", 1)];
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.NodeTypeRequired && issue.Path == "/nodes/0/type");
    }

    /// <summary>
    /// Verifies that invalid node types are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidNodeType()
    {
        WorkflowNode[] nodes = [new("start", "Core.Start", 1)];
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidNodeType && issue.Path == "/nodes/0/type");
    }

    /// <summary>
    /// Verifies that exact catalog node type casing is allowed after lowercase segment starts.
    /// </summary>
    [Fact]
    public void AcceptsCamelCaseNodeTypeSegments()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            new("select", "web.selectOption", 1),
            new("check", "web.setChecked", 1),
        ];
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("select", "main")),
            new(new WorkflowEndpoint("select", "main"), new WorkflowEndpoint("check", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: connections));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidNodeType);
    }

    /// <summary>
    /// Verifies that type versions below one are rejected.
    /// </summary>
    [Fact]
    public void RejectsTypeVersionBelowOne()
    {
        WorkflowNode[] nodes = [new("start", "core.start", 0)];
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidNodeTypeVersion && issue.Path == "/nodes/0/typeVersion");
    }

    /// <summary>
    /// Verifies that node parameters are not validated in this phase.
    /// </summary>
    [Fact]
    public void DoesNotValidateNodeParameters()
    {
        JsonObject parameters = new()
        {
            ["anything"] = new JsonObject { ["nested"] = new JsonArray(1, true, null) },
        };
        WorkflowNode[] nodes = [new("start", "core.start", 1, parameters: parameters)];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: []));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that an end node is not required.
    /// </summary>
    [Fact]
    public void DoesNotRequireAnEndNode()
    {
        WorkflowDocument workflow = ValidationTestData.CreateValidWorkflow(
            nodes: [new WorkflowNode("start", "core.start", 1), ValidationTestData.Node("log")],
            connections: [new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("log", "main"))]);

        WorkflowValidationResult result = ValidationTestData.Validate(workflow);

        Assert.True(result.IsValid);
    }
}
