using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Designer;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.Policies;
using SkeletonKey.Workflow.Specification;

namespace SkeletonKey.Serialization.Json.Tests.Support;

internal static class WorkflowJsonTestData
{
    public const string MinimalJson = """
        {
          "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
          "specVersion": "0.1.0",
          "id": "minimal",
          "name": "Minimal workflow",
          "inputs": {},
          "variables": {},
          "nodes": [
            {
              "id": "start",
              "type": "core.start",
              "typeVersion": 1,
              "disabled": false,
              "parameters": {}
            },
            {
              "id": "end",
              "type": "core.end",
              "typeVersion": 1,
              "disabled": false,
              "parameters": {}
            }
          ],
          "connections": [
            {
              "from": {
                "node": "start",
                "port": "main"
              },
              "to": {
                "node": "end",
                "port": "main"
              }
            }
          ],
          "outputs": {}
        }
        """;

    public static WorkflowDocument CreateMinimalWorkflow()
    {
        return new WorkflowDocument(
            schema: WorkflowSpecification.CurrentSchemaUri,
            specVersion: WorkflowSpecification.CurrentVersion,
            id: "minimal",
            name: "Minimal workflow",
            nodes:
            [
                new WorkflowNode("start", "core.start", 1),
                new WorkflowNode("end", "core.end", 1),
            ],
            connections:
            [
                new WorkflowConnection(
                    new WorkflowEndpoint("start", "main"),
                    new WorkflowEndpoint("end", "main")),
            ]);
    }

    public static WorkflowDocument CreateRepositoryExampleWorkflow()
    {
        return new WorkflowDocument(
            schema: WorkflowSpecification.CurrentSchemaUri,
            specVersion: WorkflowSpecification.CurrentVersion,
            id: "minimal-workflow",
            name: "Minimal Workflow",
            nodes:
            [
                new WorkflowNode("start", "core.start", 1),
                new WorkflowNode(
                    "log",
                    "core.log",
                    1,
                    parameters: new JsonObject
                    {
                        ["message"] = "Hello from SkeletonKey",
                        ["level"] = "information",
                    }),
                new WorkflowNode("end", "core.end", 1),
            ],
            connections:
            [
                new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("log", "main")),
                new WorkflowConnection(new WorkflowEndpoint("log", "main"), new WorkflowEndpoint("end", "main")),
            ],
            outputs: new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(
                    WorkflowOutputMode.Single,
                    new WorkflowEndpoint("log", "main"),
                    description: "The final example result."),
            },
            designer: new WorkflowDesignerMetadata(
                positions: new Dictionary<string, WorkflowNodePosition>
                {
                    ["start"] = new WorkflowNodePosition(0, 0),
                    ["log"] = new WorkflowNodePosition(240, 0),
                    ["end"] = new WorkflowNodePosition(480, 0),
                },
                sizes: new Dictionary<string, WorkflowNodeSize>
                {
                    ["log"] = new WorkflowNodeSize(180, 80),
                }));
    }

    public static string CreateComplexWorkflowJson()
    {
        return new WorkflowJsonSerializer().Serialize(CreateComplexWorkflow());
    }

    public static WorkflowDocument CreateComplexWorkflow()
    {
        return new WorkflowDocument(
            schema: WorkflowSpecification.CurrentSchemaUri,
            specVersion: WorkflowSpecification.CurrentVersion,
            id: "complex",
            name: "Complex workflow",
            inputs: new Dictionary<string, WorkflowInputDefinition>
            {
                ["customer"] = new WorkflowInputDefinition(
                    WorkflowInputType.Object,
                    required: true,
                    defaultValue: new JsonObject
                    {
                        ["name"] = "Ada",
                    }),
                ["optional"] = new WorkflowInputDefinition(WorkflowInputType.String, hasDefault: true),
            },
            variables: new Dictionary<string, JsonNode?>
            {
                ["text"] = "hello",
                ["count"] = 3,
                ["enabled"] = true,
                ["nothing"] = null,
                ["items"] = new JsonArray(1, "two", false),
                ["obj"] = new JsonObject
                {
                    ["nested"] = new JsonObject
                    {
                        ["value"] = 1.5,
                    },
                },
            },
            nodes:
            [
                new WorkflowNode(
                    "log",
                    "core.log",
                    1,
                    displayName: "Log",
                    disabled: true,
                    parameters: new JsonObject
                    {
                        ["message"] = "hello",
                        ["nested"] = new JsonObject
                        {
                            ["numbers"] = new JsonArray(1, 2.5),
                            ["flag"] = true,
                            ["none"] = null,
                        },
                    },
                    policy: new WorkflowExecutionPolicy("PT30S", WorkflowOnError.Continue, new WorkflowRetryPolicy(3, "PT1S", 2.0, "PT10S"))),
            ],
            connections: []);
    }
}


