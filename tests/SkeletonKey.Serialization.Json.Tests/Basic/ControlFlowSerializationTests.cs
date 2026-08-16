using SkeletonKey.Serialization.Json.Tests.Support;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Serialization.Json.Tests.Basic;

/// <summary>
/// Covers strict JSON serialization for expression and control-flow contracts.
/// </summary>
public sealed class ControlFlowSerializationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies expression wrappers round-trip and preserve expression text exactly.
    /// </summary>
    [Fact]
    public void RoundTripsExpressionWrapperAndPreservesText()
    {
        string expression = "size(inputs.items) > 0 && inputs.name == 'Ada'";
        string json = WorkflowJson("expression-wrapper", Node("check", "flow.if", $$"""
            {
              "condition": {
                "$expression": "{{expression}}"
              }
            }
            """));

        WorkflowDocument workflow = _serializer.Deserialize(json);
        string roundTrip = _serializer.Serialize(workflow);
        WorkflowDocument reparsed = _serializer.Deserialize(roundTrip);

        Assert.Equal(expression, reparsed.Nodes[1].Parameters["condition"]!["$expression"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies literal wrappers preserve expression-shaped data.
    /// </summary>
    [Fact]
    public void PreservesLiteralExpressionThroughLiteralWrapper()
    {
        string json = WorkflowJson("literal-expression", Node("return", "core.return", """
            {
              "outcome": {
                "kind": "success",
                "code": "ok",
                "data": {
                  "$literal": {
                    "$expression": "application data"
                  }
                }
              }
            }
            """));

        string roundTrip = _serializer.Serialize(_serializer.Deserialize(json));

        Assert.Contains("application data", roundTrip);
    }

    /// <summary>
    /// Verifies flow.if round-trips.
    /// </summary>
    [Fact]
    public void RoundTripsFlowIf()
    {
        AssertRoundTrips(Node("if", "flow.if", """{"condition":true}"""));
    }

    /// <summary>
    /// Verifies flow.switch round-trips and preserves case order.
    /// </summary>
    [Fact]
    public void RoundTripsFlowSwitchAndPreservesCaseOrder()
    {
        string roundTrip = AssertRoundTrips(Node("switch", "flow.switch", """
            {
              "cases": [
                {
                  "id": "phone",
                  "when": true
                },
                {
                  "id": "username",
                  "when": {
                    "$expression": "inputs.method == 'username'"
                  }
                }
              ]
            }
            """));

        Assert.True(roundTrip.IndexOf("\"phone\"", StringComparison.Ordinal) < roundTrip.IndexOf("\"username\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies flow.foreach round-trips.
    /// </summary>
    [Fact]
    public void RoundTripsFlowForEach()
    {
        AssertRoundTrips(Node("foreach", "flow.foreach", """{"items":{"$binding":{"source":"input","name":"contacts"}}}"""));
    }

    /// <summary>
    /// Verifies parallel foreach policy round-trips.
    /// </summary>
    [Fact]
    public void RoundTripsParallelForEachPolicy()
    {
        AssertRoundTrips(Node("foreach", "flow.foreach", """{"items":[],"execution":{"mode":"parallel","maxConcurrency":4}}"""));
    }

    /// <summary>
    /// Verifies flow.repeat round-trips.
    /// </summary>
    [Fact]
    public void RoundTripsFlowRepeat()
    {
        AssertRoundTrips(Node("repeat", "flow.repeat", """{"count":3}"""));
    }

    /// <summary>
    /// Verifies flow.while round-trips.
    /// </summary>
    [Fact]
    public void RoundTripsFlowWhile()
    {
        AssertRoundTrips(Node("while", "flow.while", """{"condition":{"$expression":"inputs.keepGoing"},"maxIterations":10}"""));
    }

    /// <summary>
    /// Verifies core.return round-trips.
    /// </summary>
    [Fact]
    public void RoundTripsCoreReturn()
    {
        AssertRoundTrips(Node("return", "core.return", """{"outcome":{"kind":"skipped","code":"not-needed"}}"""));
    }

    /// <summary>
    /// Verifies iteration bindings round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsIterationBinding()
    {
        AssertRoundTrips(Node("return", "core.return", """
            {
              "outcome": {
                "kind": "success",
                "code": "ok",
                "data": {
                  "$binding": {
                    "source": "iteration",
                    "iteration": "foreach",
                    "path": "/item"
                  }
                }
              }
            }
            """));
    }

    /// <summary>
    /// Verifies duplicate expression wrapper properties are rejected by strict parsing.
    /// </summary>
    [Fact]
    public void RejectsDuplicateExpressionWrapperProperty()
    {
        string json = WorkflowJson("duplicate-expression", Node("if", "flow.if", """
            {
              "condition": {
                "$expression": "true",
                "$expression": "false"
              }
            }
            """));

        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(json));
    }

    /// <summary>
    /// Verifies duplicate switch case properties are rejected by strict parsing.
    /// </summary>
    [Fact]
    public void RejectsDuplicateSwitchCaseProperty()
    {
        string json = WorkflowJson("duplicate-switch-case", Node("switch", "flow.switch", """
            {
              "cases": [
                {
                  "id": "one",
                  "id": "two",
                  "when": true
                }
              ]
            }
            """));

        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(json));
    }

    /// <summary>
    /// Verifies unknown control parameters are preserved for schema and semantic validation boundaries.
    /// </summary>
    [Fact]
    public void DeserializesUnknownControlParameterForLaterValidation()
    {
        string json = WorkflowJson("unknown-control-parameter", Node("if", "flow.if", """{"condition":true,"actions":[]}"""));

        WorkflowDocument workflow = _serializer.Deserialize(json);

        Assert.True(workflow.Nodes[1].Parameters.ContainsKey("actions"));
    }

    /// <summary>
    /// Verifies control parameter order is canonicalized.
    /// </summary>
    [Fact]
    public void SerializesControlParametersInCanonicalOrder()
    {
        string json = WorkflowJson("canonical-control", Node("return", "core.return", """
            {
              "outcome": {
                "data": 1,
                "message": "done",
                "code": "ok",
                "kind": "success"
              }
            }
            """));

        string roundTrip = _serializer.Serialize(_serializer.Deserialize(json));

        Assert.True(roundTrip.IndexOf("\"kind\"", StringComparison.Ordinal) < roundTrip.IndexOf("\"code\"", StringComparison.Ordinal));
        Assert.True(roundTrip.IndexOf("\"code\"", StringComparison.Ordinal) < roundTrip.IndexOf("\"message\"", StringComparison.Ordinal));
        Assert.True(roundTrip.IndexOf("\"message\"", StringComparison.Ordinal) < roundTrip.IndexOf("\"data\"", StringComparison.Ordinal));
    }

    private string AssertRoundTrips(string nodeJson)
    {
        string json = WorkflowJson("roundtrip", nodeJson);
        WorkflowDocument workflow = _serializer.Deserialize(json);
        string first = _serializer.Serialize(workflow);
        string second = _serializer.Serialize(_serializer.Deserialize(first));
        Assert.Equal(first, second);
        return first;
    }

    private static string Node(string id, string type, string parameters)
    {
        return $$"""
            {
              "id": "{{id}}",
              "type": "{{type}}",
              "typeVersion": 1,
              "disabled": false,
              "parameters": {{parameters}}
            }
            """;
    }

    private static string WorkflowJson(string id, string nodeJson)
    {
        return $$"""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "{{id}}",
              "name": "{{id}}",
              "inputs": {
                "contacts": {
                  "type": "array"
                },
                "keepGoing": {
                  "type": "boolean"
                },
                "method": {
                  "type": "string"
                }
              },
              "variables": {},
              "nodes": [
                {
                  "id": "start",
                  "type": "core.start",
                  "typeVersion": 1,
                  "disabled": false,
                  "parameters": {}
                },
                {{nodeJson}},
                {
                  "id": "end",
                  "type": "core.end",
                  "typeVersion": 1,
                  "disabled": false,
                  "parameters": {}
                }
              ],
              "connections": [],
              "outputs": {}
            }
            """;
    }
}
