using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers workflow connection validation.
/// </summary>
public sealed class ConnectionValidationTests
{
    /// <summary>
    /// Verifies that a valid connection is accepted.
    /// </summary>
    [Fact]
    public void AcceptsAValidConnection()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow());

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that unknown source nodes are rejected.
    /// </summary>
    [Fact]
    public void RejectsUnknownSourceNode()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("missing", "main"), new WorkflowEndpoint("end", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.UnknownSourceNode && issue.Path == "/connections/0/from/node");
    }

    /// <summary>
    /// Verifies that empty source nodes are rejected.
    /// </summary>
    [Fact]
    public void RejectsEmptySourceNode()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("", "main"), new WorkflowEndpoint("end", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.SourceNodeRequired && issue.Path == "/connections/0/from/node");
    }

    /// <summary>
    /// Verifies that unknown target nodes are rejected.
    /// </summary>
    [Fact]
    public void RejectsUnknownTargetNode()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("missing", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.UnknownTargetNode && issue.Path == "/connections/0/to/node");
    }

    /// <summary>
    /// Verifies that empty target nodes are rejected.
    /// </summary>
    [Fact]
    public void RejectsEmptyTargetNode()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint(" ", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.TargetNodeRequired && issue.Path == "/connections/0/to/node");
    }

    /// <summary>
    /// Verifies that invalid source ports are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidSourcePort()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "bad port"), new WorkflowEndpoint("end", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidSourcePort && issue.Path == "/connections/0/from/port");
    }

    /// <summary>
    /// Verifies that invalid target ports are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidTargetPort()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("end", "")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidTargetPort && issue.Path == "/connections/0/to/port");
    }

    /// <summary>
    /// Verifies that duplicate connections are rejected.
    /// </summary>
    [Fact]
    public void RejectsDuplicateConnection()
    {
        WorkflowConnection connection = new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("end", "main"));

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: [connection, connection]));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.DuplicateConnection && issue.Path == "/connections/1");
    }

    /// <summary>
    /// Verifies that connection identity is case-sensitive.
    /// </summary>
    [Fact]
    public void TreatsConnectionsAsCaseSensitive()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            ValidationTestData.Node("Log"),
            ValidationTestData.Node("log"),
        ];
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("Log", "main")),
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("log", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: connections));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.DuplicateConnection);
    }

    /// <summary>
    /// Verifies that incoming connections to start nodes are rejected.
    /// </summary>
    [Fact]
    public void RejectsIncomingConnectionToStart()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("end", "main"), new WorkflowEndpoint("start", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.IncomingConnectionToStartNode && issue.Path == "/connections/0/to/node");
    }

    /// <summary>
    /// Verifies that outgoing connections from end nodes are rejected.
    /// </summary>
    [Fact]
    public void RejectsOutgoingConnectionFromEnd()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("end", "main"), new WorkflowEndpoint("start", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.OutgoingConnectionFromEndNode && issue.Path == "/connections/0/from/node");
    }

    /// <summary>
    /// Verifies that cycles are not rejected in this phase.
    /// </summary>
    [Fact]
    public void DoesNotRejectCycles()
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

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that self-connections are not rejected in this phase.
    /// </summary>
    [Fact]
    public void DoesNotRejectSelfConnections()
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            ValidationTestData.Node("a"),
        ];
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("a", "main")),
            new(new WorkflowEndpoint("a", "main"), new WorkflowEndpoint("a", "main")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: connections));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that port existence is not validated in this phase.
    /// </summary>
    [Fact]
    public void DoesNotValidatePortExistence()
    {
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "notRegisteredYet"), new WorkflowEndpoint("end", "alsoNotRegisteredYet")),
        ];

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(connections: connections));

        Assert.True(result.IsValid);
    }
}
