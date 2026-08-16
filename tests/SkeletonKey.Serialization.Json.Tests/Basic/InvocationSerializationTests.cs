using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Serialization.Json.Tests.Basic;

/// <summary>
/// Covers workflow invocation and structured binding JSON serialization.
/// </summary>
public sealed class InvocationSerializationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies workflow.invoke nodes round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsWorkflowInvokeNode()
    {
        string json = RoundTrip(CreateWorkflow(InvocationParameters()));

        Assert.Contains("\"type\": \"workflow.invoke\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies workflow references with versions round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsWorkflowReferenceWithVersion()
    {
        string json = RoundTrip(CreateWorkflow(InvocationParameters()));

        Assert.Contains("\"version\": \"1.0.0\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies workflow references without versions round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsWorkflowReferenceWithoutVersion()
    {
        JsonObject parameters = InvocationParameters();
        parameters["workflow"]!.AsObject().Remove("version");

        string json = RoundTrip(CreateWorkflow(parameters));

        Assert.Contains("\"id\": \"child-workflow\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"version\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies input bindings round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsInputBinding()
    {
        JsonObject parameters = InvocationParameters();
        parameters["inputs"]!["account"] = Binding("input", "account");

        Assert.Contains("\"source\": \"input\"", RoundTrip(CreateWorkflow(parameters)), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies variable bindings round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsVariableBinding()
    {
        JsonObject parameters = InvocationParameters();
        parameters["inputs"]!["message"] = Binding("variable", "message");

        Assert.Contains("\"source\": \"variable\"", RoundTrip(CreateWorkflow(parameters)), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies node bindings round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsNodeBinding()
    {
        JsonObject parameters = InvocationParameters();
        parameters["inputs"]!["result"] = NodeBinding();

        Assert.Contains("\"source\": \"node\"", RoundTrip(CreateWorkflow(parameters)), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies nested bindings round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsNestedBindings()
    {
        JsonObject parameters = InvocationParameters();
        parameters["inputs"]!["payload"] = new JsonObject
        {
            ["items"] = new JsonArray(Binding("input", "account"), Binding("variable", "message")),
        };

        string json = RoundTrip(CreateWorkflow(parameters));

        Assert.Contains("\"items\"", json, StringComparison.Ordinal);
        Assert.Contains("\"variable\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies literal wrappers round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsLiteralWrapper()
    {
        JsonObject parameters = InvocationParameters();
        parameters["inputs"]!["literal"] = LiteralReservedObject();

        Assert.Contains("\"$literal\"", RoundTrip(CreateWorkflow(parameters)), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies explicit null binding defaults round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsExplicitNullBindingDefault()
    {
        JsonObject binding = Binding("input", "account");
        binding["$binding"]!["onMissing"] = "default";
        binding["$binding"]!["default"] = null;
        JsonObject parameters = InvocationParameters();
        parameters["inputs"]!["account"] = binding;

        string json = RoundTrip(CreateWorkflow(parameters));

        Assert.Contains("\"default\": null", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies invocation input order is preserved.
    /// </summary>
    [Fact]
    public void PreservesInvocationInputOrder()
    {
        string json = _serializer.Serialize(CreateWorkflow(InvocationParameters()), indented: false);

        AssertInOrder(json, "\"account\"", "\"message\"", "\"result\"");
    }

    /// <summary>
    /// Verifies stream mapping order is preserved.
    /// </summary>
    [Fact]
    public void PreservesStreamMappingOrder()
    {
        string json = _serializer.Serialize(CreateWorkflow(InvocationParameters()), indented: false);

        AssertInOrder(json, "\"child.one\"", "\"child.two\"");
    }

    /// <summary>
    /// Verifies ordinary literal parameter objects are preserved for non-invocation nodes.
    /// </summary>
    [Fact]
    public void PreservesOrdinaryLiteralParameterObjects()
    {
        WorkflowDocument workflow = new(
            id: "literal",
            name: "Literal",
            nodes:
            [
                new WorkflowNode(
                    "start",
                    "core.start",
                    1,
                    parameters: new JsonObject
                    {
                        ["data"] = new JsonObject
                        {
                            ["$binding"] = "literal",
                        },
                    }),
            ],
            connections: []);

        string json = RoundTrip(workflow);

        Assert.Contains("\"$binding\": \"literal\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies reserved objects are preserved through literal wrappers.
    /// </summary>
    [Fact]
    public void PreservesReservedObjectThroughLiteralWrapper()
    {
        JsonObject parameters = InvocationParameters();
        parameters["inputs"]!["literal"] = LiteralReservedObject();

        string json = RoundTrip(CreateWorkflow(parameters));

        Assert.Contains("\"this\": \"is literal application data\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies duplicate properties inside bindings are rejected.
    /// </summary>
    [Fact]
    public void RejectsDuplicatePropertyInsideBinding()
    {
        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "duplicate-binding",
              "name": "Duplicate binding",
              "nodes": [
                {
                  "id": "start",
                  "type": "core.start",
                  "typeVersion": 1,
                  "parameters": {}
                },
                {
                  "id": "invoke",
                  "type": "workflow.invoke",
                  "typeVersion": 1,
                  "parameters": {
                    "workflow": { "id": "child" },
                    "inputs": {
                      "value": {
                        "$binding": {
                          "source": "input",
                          "source": "variable",
                          "name": "account"
                        }
                      }
                    }
                  }
                }
              ],
              "connections": []
            }
            """));
    }

    /// <summary>
    /// Verifies duplicate properties inside invocation workflow references are rejected.
    /// </summary>
    [Fact]
    public void RejectsDuplicatePropertyInsideInvocationWorkflowReference()
    {
        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "duplicate-reference",
              "name": "Duplicate reference",
              "nodes": [
                {
                  "id": "invoke",
                  "type": "workflow.invoke",
                  "typeVersion": 1,
                  "parameters": {
                    "workflow": {
                      "id": "child",
                      "id": "other"
                    }
                  }
                }
              ],
              "connections": []
            }
            """));
    }

    private string RoundTrip(WorkflowDocument workflow)
    {
        string first = _serializer.Serialize(workflow);
        string second = _serializer.Serialize(_serializer.Deserialize(first));
        Assert.Equal(first, second);
        return second;
    }

    private static WorkflowDocument CreateWorkflow(JsonObject parameters)
    {
        return new WorkflowDocument(
            id: "parent",
            name: "Parent",
            inputs: new Dictionary<string, WorkflowInputDefinition>
            {
                ["account"] = new WorkflowInputDefinition(WorkflowInputType.Object),
            },
            variables: new Dictionary<string, JsonNode?>
            {
                ["message"] = "hello",
            },
            nodes:
            [
                new WorkflowNode("start", "core.start", 1),
                new WorkflowNode("previous", "core.log", 1),
                new WorkflowNode("invoke", "workflow.invoke", 1, parameters: parameters),
            ],
            connections:
            [
                new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("previous", "main")),
                new WorkflowConnection(new WorkflowEndpoint("previous", "main"), new WorkflowEndpoint("invoke", "main")),
            ]);
    }

    private static JsonObject InvocationParameters()
    {
        return new JsonObject
        {
            ["workflow"] = new JsonObject
            {
                ["id"] = "child-workflow",
                ["version"] = "1.0.0",
            },
            ["inputs"] = new JsonObject
            {
                ["account"] = Binding("input", "account"),
                ["message"] = Binding("variable", "message"),
                ["result"] = NodeBinding(),
            },
            ["streams"] = new JsonObject
            {
                ["mode"] = "map",
                ["mappings"] = new JsonObject
                {
                    ["child.one"] = "parent.one",
                    ["child.two"] = "parent.two",
                },
            },
        };
    }

    private static JsonObject Binding(string source, string name)
    {
        return new JsonObject
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = source,
                ["name"] = name,
            },
        };
    }

    private static JsonObject NodeBinding()
    {
        return new JsonObject
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = "node",
                ["node"] = "previous",
                ["port"] = "result",
                ["path"] = "/outputs/loggedOut",
            },
        };
    }

    private static JsonObject LiteralReservedObject()
    {
        return new JsonObject
        {
            ["$literal"] = new JsonObject
            {
                ["$binding"] = new JsonObject
                {
                    ["this"] = "is literal application data",
                },
            },
        };
    }

    private static void AssertInOrder(string text, params string[] tokens)
    {
        int currentIndex = -1;
        foreach (string token in tokens)
        {
            int nextIndex = text.IndexOf(token, currentIndex + 1, StringComparison.Ordinal);
            Assert.True(nextIndex > currentIndex, $"Token '{token}' did not appear after index {currentIndex} in: {text}");
            currentIndex = nextIndex;
        }
    }
}
