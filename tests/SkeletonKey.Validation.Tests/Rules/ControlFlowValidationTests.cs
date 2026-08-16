using System.Text.Json.Nodes;
using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers semantic validation for control-flow, iteration, and expression contracts.
/// </summary>
public sealed class ControlFlowValidationTests
{
    /// <summary>
    /// Verifies valid condition forms are accepted.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("""{"$binding":{"source":"input","name":"enabled"}}""")]
    [InlineData("""{"$expression":"inputs.enabled"}""")]
    public void AcceptsValidFlowIfConditionForms(string conditionJson)
    {
        WorkflowValidationResult result = Validate(Node("branch", "flow.if", $$"""{"condition":{{conditionJson}}}"""), Inputs(("enabled", WorkflowInputType.Boolean)));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Verifies invalid literal conditions are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidLiteralCondition()
    {
        WorkflowValidationResult result = Validate(Node("branch", "flow.if", """{"condition":"yes"}"""));

        Assert.Contains(result.Errors, issue => issue.Code == WorkflowValidationCodes.InvalidConditionValue);
    }

    /// <summary>
    /// Verifies valid switch declarations are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidSwitch()
    {
        WorkflowValidationResult result = Validate(Node("select", "flow.switch", """{"cases":[{"id":"phone","when":true},{"id":"username","when":{"$expression":"inputs.method == 'username'"}}]}"""), Inputs(("method", WorkflowInputType.String)));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies empty switch cases are rejected.
    /// </summary>
    [Fact]
    public void RejectsEmptySwitch()
    {
        WorkflowValidationResult result = Validate(Node("select", "flow.switch", """{"cases":[]}"""));

        Assert.Contains(result.Errors, issue => issue.Code == WorkflowValidationCodes.MissingSwitchCases);
    }

    /// <summary>
    /// Verifies invalid, reserved, and duplicate switch case IDs are rejected.
    /// </summary>
    [Theory]
    [InlineData("1bad", WorkflowValidationCodes.InvalidSwitchCaseId)]
    [InlineData("default", WorkflowValidationCodes.InvalidSwitchCaseId)]
    [InlineData("duplicate", WorkflowValidationCodes.DuplicateSwitchCaseId)]
    public void RejectsInvalidSwitchCaseIds(string scenario, string expectedCode)
    {
        string cases = scenario == "duplicate"
            ? """[{"id":"phone","when":true},{"id":"phone","when":false}]"""
            : $$"""[{"id":"{{scenario}}","when":true}]""";

        WorkflowValidationResult result = Validate(Node("select", "flow.switch", $$"""{"cases":{{cases}}}"""));

        Assert.Contains(result.Errors, issue => issue.Code == expectedCode);
    }

    /// <summary>
    /// Verifies sequential and parallel foreach policies are accepted.
    /// </summary>
    [Theory]
    [InlineData("""{"items":[]}""")]
    [InlineData("""{"items":[],"execution":{"mode":"sequential"}}""")]
    [InlineData("""{"items":[],"execution":{"mode":"parallel","maxConcurrency":2}}""")]
    public void AcceptsValidForEachPolicies(string parameters)
    {
        WorkflowValidationResult result = Validate(Node("loop", "flow.foreach", parameters), extraConnections: LoopConnections("loop"));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies invalid foreach execution policies are rejected.
    /// </summary>
    [Theory]
    [InlineData("""{"items":[],"execution":{"mode":"sequential","maxConcurrency":2}}""")]
    [InlineData("""{"items":[],"execution":{"mode":"parallel"}}""")]
    [InlineData("""{"items":[],"execution":{"mode":"parallel","maxConcurrency":0}}""")]
    public void RejectsInvalidForEachPolicies(string parameters)
    {
        WorkflowValidationResult result = Validate(Node("loop", "flow.foreach", parameters), extraConnections: LoopConnections("loop"));

        Assert.Contains(result.Errors, issue => issue.Code == WorkflowValidationCodes.InvalidForEachExecutionPolicy);
    }

    /// <summary>
    /// Verifies valid and invalid repeat counts.
    /// </summary>
    [Theory]
    [InlineData("0", true)]
    [InlineData("""{"$expression":"inputs.count"}""", true)]
    [InlineData("-1", false)]
    public void ValidatesRepeatCount(string count, bool expectedValid)
    {
        WorkflowValidationResult result = Validate(Node("repeat", "flow.repeat", $$"""{"count":{{count}}}"""), Inputs(("count", WorkflowInputType.Integer)), LoopConnections("repeat"));

        Assert.Equal(expectedValid, result.Errors.All(issue => issue.Code != WorkflowValidationCodes.InvalidRepeatCount));
    }

    /// <summary>
    /// Verifies valid while declarations are accepted and invalid limits are rejected.
    /// </summary>
    [Theory]
    [InlineData("""{"condition":true}""", true)]
    [InlineData("""{"condition":{"$expression":"inputs.keepGoing"},"maxIterations":10}""", true)]
    [InlineData("""{"condition":true,"maxIterations":0}""", false)]
    public void ValidatesWhileNode(string parameters, bool expectedValid)
    {
        WorkflowValidationResult result = Validate(Node("while", "flow.while", parameters), Inputs(("keepGoing", WorkflowInputType.Boolean)), LoopConnections("while"));

        Assert.Equal(expectedValid, result.Errors.All(issue => issue.Code != WorkflowValidationCodes.InvalidWhileIterationLimit));
    }

    /// <summary>
    /// Verifies valid iteration bindings are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidIterationBinding()
    {
        WorkflowNode loop = Node("loop", "flow.foreach", """{"items":[]}""");
        WorkflowNode ret = Node("return", "core.return", """{"outcome":{"kind":"success","code":"ok","data":{"$binding":{"source":"iteration","iteration":"loop","path":"/item"}}}}""");

        WorkflowValidationResult result = Validate([loop, ret], connections: [.. LoopConnections("loop"), new(new WorkflowEndpoint("loop", "completed"), new WorkflowEndpoint("return", "main"))]);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies unknown iteration bindings are rejected.
    /// </summary>
    [Fact]
    public void RejectsUnknownIterationBinding()
    {
        WorkflowValidationResult result = Validate(Node("return", "core.return", """{"outcome":{"kind":"success","code":"ok","data":{"$binding":{"source":"iteration","iteration":"missing"}}}}"""), returnWorkflow: true);

        Assert.Contains(result.Errors, issue => issue.Code == WorkflowValidationCodes.UnknownIterationReference);
    }

    /// <summary>
    /// Verifies valid iteration expression references are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidIterationExpressionReference()
    {
        WorkflowNode loop = Node("loop", "flow.foreach", """{"items":[]}""");
        WorkflowNode branch = Node("branch", "flow.if", """{"condition":{"$expression":"iterations['loop'].index >= 0"}}""");

        WorkflowValidationResult result = Validate([loop, branch], Inputs(), [.. LoopConnections("loop"), new(new WorkflowEndpoint("loop", "body"), new WorkflowEndpoint("branch", "main"))]);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies malformed and unknown-reference expressions are rejected.
    /// </summary>
    [Theory]
    [InlineData("inputs.", WorkflowValidationCodes.ExpressionSyntaxError)]
    [InlineData("inputs.missing", WorkflowValidationCodes.UnknownExpressionInput)]
    [InlineData("variables.missing", WorkflowValidationCodes.UnknownExpressionVariable)]
    [InlineData("nodes['missing'].outputs.result", WorkflowValidationCodes.UnknownExpressionNode)]
    [InlineData("iterations['missing'].index", WorkflowValidationCodes.UnknownIterationReference)]
    [InlineData("eval(inputs.enabled)", WorkflowValidationCodes.UnknownExpressionFunction)]
    public void RejectsInvalidExpressions(string expression, string expectedCode)
    {
        WorkflowValidationResult result = Validate(Node("branch", "flow.if", "{\"condition\":{\"$expression\":\"" + expression + "\"}}"), Inputs(("enabled", WorkflowInputType.Boolean)));

        Assert.Contains(result.Errors, issue => issue.Code == expectedCode);
    }

    /// <summary>
    /// Verifies node expressions cannot reference the same node.
    /// </summary>
    [Fact]
    public void RejectsExpressionSelfReference()
    {
        WorkflowValidationResult result = Validate(Node("branch", "flow.if", """{"condition":{"$expression":"nodes['branch'].outputs.result"}}"""));

        Assert.Contains(result.Errors, issue => issue.Code == WorkflowValidationCodes.SelfReferencingExpressionNode);
    }

    /// <summary>
    /// Verifies valid return declarations are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidReturn()
    {
        WorkflowValidationResult result = Validate(Node("return", "core.return", """{"outcome":{"kind":"requires-action","code":"account.logged-out","message":"Login required","data":{"reason":"expired"}}}"""), returnWorkflow: true);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies invalid return outcomes are rejected.
    /// </summary>
    [Theory]
    [InlineData("""{"outcome":{"kind":"bad","code":"ok"}}""")]
    [InlineData("""{"outcome":{"kind":"success","code":""}}""")]
    [InlineData("""{"outcome":{"kind":"success","code":"ok","message":1}}""")]
    public void RejectsInvalidReturnOutcome(string parameters)
    {
        WorkflowValidationResult result = Validate(Node("return", "core.return", parameters), returnWorkflow: true);

        Assert.Contains(result.Errors, issue => issue.Code == WorkflowValidationCodes.InvalidReturnOutcome);
    }

    /// <summary>
    /// Verifies outgoing return connections are rejected.
    /// </summary>
    [Fact]
    public void RejectsOutgoingConnectionFromReturn()
    {
        WorkflowNode ret = Node("return", "core.return", """{"outcome":{"kind":"success","code":"ok"}}""");
        WorkflowValidationResult result = Validate([ret, Node("end", "core.end", "{}")], connections:
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("return", "main")),
            new(new WorkflowEndpoint("return", "main"), new WorkflowEndpoint("end", "main")),
        ]);

        Assert.Contains(result.Errors, issue => issue.Code == WorkflowValidationCodes.OutgoingConnectionFromReturn);
    }

    /// <summary>
    /// Verifies invalid reserved output ports are rejected.
    /// </summary>
    [Theory]
    [InlineData("flow.if", "maybe", WorkflowValidationCodes.InvalidConditionalOutputPort)]
    [InlineData("flow.switch", "missing", WorkflowValidationCodes.InvalidConditionalOutputPort)]
    [InlineData("flow.foreach", "next", WorkflowValidationCodes.InvalidLoopControlPort)]
    public void RejectsInvalidReservedOutputPorts(string type, string outputPort, string expectedCode)
    {
        string parameters = type switch
        {
            "flow.if" => """{"condition":true}""",
            "flow.switch" => """{"cases":[{"id":"phone","when":true}]}""",
            _ => """{"items":[]}""",
        };

        WorkflowValidationResult result = Validate(Node("node", type, parameters), extraConnections:
        [
            new(new WorkflowEndpoint("node", outputPort), new WorkflowEndpoint("end", "main")),
        ]);

        Assert.Contains(result.Errors, issue => issue.Code == expectedCode);
    }

    /// <summary>
    /// Verifies invalid reserved input ports are rejected.
    /// </summary>
    [Theory]
    [InlineData("flow.if", "continue", WorkflowValidationCodes.InvalidReservedControlInputPort)]
    [InlineData("flow.foreach", "other", WorkflowValidationCodes.InvalidLoopControlPort)]
    public void RejectsInvalidReservedInputPorts(string type, string inputPort, string expectedCode)
    {
        string parameters = type == "flow.if" ? """{"condition":true}""" : """{"items":[]}""";
        WorkflowValidationResult result = Validate(Node("node", type, parameters), extraConnections:
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("node", inputPort)),
        ]);

        Assert.Contains(result.Errors, issue => issue.Code == expectedCode);
    }

    /// <summary>
    /// Verifies validation does not mutate expression or control data.
    /// </summary>
    [Fact]
    public void ValidationDoesNotMutateExpressionOrControlData()
    {
        WorkflowNode node = Node("branch", "flow.if", """{"condition":{"$expression":"inputs.enabled"}}""");
        string before = node.Parameters.ToJsonString();

        _ = Validate(node, Inputs(("enabled", WorkflowInputType.Boolean)));

        Assert.Equal(before, node.Parameters.ToJsonString());
    }

    /// <summary>
    /// Verifies diagnostic ordering is deterministic.
    /// </summary>
    [Fact]
    public void DiagnosticOrderingIsDeterministic()
    {
        WorkflowDocument workflow = CreateWorkflow(Node("branch", "flow.if", """{"condition":{"$expression":"inputs.missing && variables.missing"}}"""));

        string[] first = [.. ValidationTestData.Validate(workflow).Errors.Select(static issue => issue.Code)];
        string[] second = [.. ValidationTestData.Validate(workflow).Errors.Select(static issue => issue.Code)];

        Assert.Equal(first, second);
    }

    private static WorkflowValidationResult Validate(
        WorkflowNode node,
        IReadOnlyDictionary<string, WorkflowInputDefinition>? inputs = null,
        IReadOnlyList<WorkflowConnection>? extraConnections = null,
        bool returnWorkflow = false)
    {
        IReadOnlyList<WorkflowConnection> connections = returnWorkflow
            ? [new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint(node.Id, "main"))]
            : extraConnections ?? [new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint(node.Id, "main")), new(new WorkflowEndpoint(node.Id, DefaultOutputPort(node.Type)), new WorkflowEndpoint("end", "main"))];

        return ValidationTestData.Validate(CreateWorkflow(node, inputs, connections));
    }

    private static WorkflowValidationResult Validate(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyDictionary<string, WorkflowInputDefinition>? inputs = null,
        IReadOnlyList<WorkflowConnection>? connections = null)
    {
        return ValidationTestData.Validate(CreateWorkflow(nodes, inputs, connections));
    }

    private static WorkflowDocument CreateWorkflow(
        WorkflowNode node,
        IReadOnlyDictionary<string, WorkflowInputDefinition>? inputs = null,
        IReadOnlyList<WorkflowConnection>? connections = null)
    {
        return CreateWorkflow([node], inputs, connections);
    }

    private static WorkflowDocument CreateWorkflow(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyDictionary<string, WorkflowInputDefinition>? inputs = null,
        IReadOnlyList<WorkflowConnection>? connections = null)
    {
        List<WorkflowNode> allNodes = [new("start", "core.start", 1), .. nodes];
        if (!nodes.Any(static node => node.Id == "end"))
        {
            allNodes.Add(new WorkflowNode("end", "core.end", 1));
        }

        return ValidationTestData.CreateValidWorkflow(
            inputs: inputs,
            variables: new Dictionary<string, JsonNode?>
            {
                ["expected"] = "yes",
            },
            nodes: allNodes,
            connections: connections ?? []);
    }

    private static WorkflowNode Node(string id, string type, string parameters)
    {
        return new WorkflowNode(id, type, 1, parameters: (JsonObject)JsonNode.Parse(parameters)!);
    }

    private static IReadOnlyDictionary<string, WorkflowInputDefinition> Inputs(params (string Name, WorkflowInputType Type)[] inputs)
    {
        return inputs.ToDictionary(static input => input.Name, static input => new WorkflowInputDefinition(input.Type), StringComparer.Ordinal);
    }

    private static WorkflowConnection[] LoopConnections(string loopId)
    {
        return
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint(loopId, "main")),
            new(new WorkflowEndpoint(loopId, "completed"), new WorkflowEndpoint("end", "main")),
        ];
    }

    private static string DefaultOutputPort(string nodeType)
    {
        return nodeType switch
        {
            "flow.if" => "true",
            "flow.switch" => "default",
            "flow.foreach" or "flow.repeat" or "flow.while" => "completed",
            _ => "main",
        };
    }
}
