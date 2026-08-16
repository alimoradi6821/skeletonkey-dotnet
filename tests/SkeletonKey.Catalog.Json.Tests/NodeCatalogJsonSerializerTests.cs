using System.Text;
using System.Text.Json.Nodes;

namespace SkeletonKey.Catalog.Json.Tests;

/// <summary>
/// Covers strict canonical node catalog JSON serialization.
/// </summary>
public sealed class NodeCatalogJsonSerializerTests
{
    private readonly NodeCatalogJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies canonical serialization uses stable property order and one LF.
    /// </summary>
    [Fact]
    public void SerializeUsesCanonicalOrderAndSingleTrailingLf()
    {
        NodeCatalogDocument document = new(
            id: "catalog",
            version: "1.0.0",
            definitions:
            [
                new(
                    "core.log",
                    1,
                    parametersSchema: new JsonObject { ["type"] = "object" },
                    inputs: new Dictionary<string, WorkflowPortDefinition> { ["main"] = new("main", WorkflowPortDirection.Input) },
                    outputs: new Dictionary<string, WorkflowPortDefinition> { ["result"] = new("result", WorkflowPortDirection.Output) },
                    capabilities: ["logging.write"]),
            ]);

        string json = _serializer.Serialize(document);

        Assert.StartsWith("{\n  \"$schema\":", json, StringComparison.Ordinal);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        Assert.False(json.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.False(Encoding.UTF8.GetPreamble().AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(json).AsSpan(0, Math.Min(3, json.Length))));
    }

    /// <summary>
    /// Verifies strict parsing rejects duplicate properties.
    /// </summary>
    [Fact]
    public void DeserializeRejectsDuplicateProperties()
    {
        string json = """
        {
          "$schema": "https://schemas.skeletonkey.dev/node-catalog/0.1/schema.json",
          "specVersion": "0.1.0",
          "id": "catalog",
          "id": "other",
          "version": "1.0.0",
          "definitions": []
        }
        """;

        Assert.Throws<NodeCatalogSerializationException>(() => _serializer.Deserialize(json));
    }

    /// <summary>
    /// Verifies deserialization preserves dynamic ports and behavior metadata.
    /// </summary>
    [Fact]
    public void DeserializePreservesDynamicPortsAndBehaviorMetadata()
    {
        string json = """
        {
          "$schema": "https://schemas.skeletonkey.dev/node-catalog/0.1/schema.json",
          "specVersion": "0.1.0",
          "id": "catalog",
          "version": "1.0.0",
          "definitions": [
            {
              "type": "flow.switch",
              "typeVersion": 1,
              "stability": "preview",
              "capabilities": [],
              "behavior": { "kind": "branch", "terminal": false, "maySuspend": false },
              "deprecation": { "deprecated": false },
              "parameterExamples": [],
              "inputs": {},
              "outputs": {},
              "dynamicPorts": [
                { "kind": "switch-cases", "direction": "output", "sourcePointer": "/cases", "idPointer": "/id" }
              ],
              "resources": {}
            }
          ]
        }
        """;

        NodeCatalogDocument document = _serializer.Deserialize(json);

        Assert.Single(document.Definitions[0].DynamicPorts);
        Assert.Equal(WorkflowNodeBehaviorKind.Branch, document.Definitions[0].Behavior.Kind);
    }
}
