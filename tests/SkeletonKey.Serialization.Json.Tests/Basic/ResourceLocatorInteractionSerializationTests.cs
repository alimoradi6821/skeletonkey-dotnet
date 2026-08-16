using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Serialization.Json.Tests.Basic;

/// <summary>
/// Covers resource, locator, and interaction JSON serialization.
/// </summary>
public sealed class ResourceLocatorInteractionSerializationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies workflow resources and constraints round-trip and preserve order.
    /// </summary>
    [Fact]
    public void RoundTripsWorkflowResources()
    {
        WorkflowDocument workflow = Workflow(resources: new Dictionary<string, WorkflowResourceDefinition>
        {
            ["browser"] = new WorkflowResourceDefinition(
                StandardWorkflowResourceKinds.WebBrowser,
                WorkflowResourceLifetime.Execution,
                WorkflowResourceAccessMode.Shared,
                capabilities: [StandardWorkflowResourceCapabilities.WebPersistentProfile],
                constraints: new JsonObject { ["engine"] = "chromium" }),
            ["interaction"] = new WorkflowResourceDefinition(StandardWorkflowResourceKinds.InteractionHandler),
        });

        string json = RoundTrip(workflow);

        AssertInOrder(json, "\"variables\"", "\"resources\"", "\"nodes\"");
        AssertInOrder(json, "\"browser\"", "\"interaction\"");
        Assert.Contains("\"constraints\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies resource and locator reference wrappers round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsResourceAndLocatorReferenceWrappers()
    {
        WorkflowDocument workflow = Workflow(node: new WorkflowNode("start", "core.start", 1, parameters: new JsonObject
        {
            ["resource"] = Resource("browser"),
            ["locator"] = Locator("catalog", "1.0.0", "save"),
        }));

        string json = RoundTrip(workflow);

        Assert.Contains("\"$resource\"", json, StringComparison.Ordinal);
        Assert.Contains("\"$locator\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies invocation resource mappings round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsInvocationResourceMapping()
    {
        WorkflowDocument workflow = Workflow(node: new WorkflowNode("invoke", "workflow.invoke", 1, parameters: new JsonObject
        {
            ["streams"] = new JsonObject { ["mode"] = "forward" },
            ["resources"] = new JsonObject { ["browser"] = Resource("browser") },
            ["inputs"] = new JsonObject(),
            ["workflow"] = new JsonObject { ["id"] = "child" },
        }));

        string json = RoundTrip(workflow);

        AssertInOrder(json, "\"workflow\"", "\"inputs\"", "\"resources\"", "\"streams\"");
    }

    /// <summary>
    /// Verifies interaction request nodes preserve option order and explicit null defaults.
    /// </summary>
    [Fact]
    public void RoundTripsInteractionRequestNode()
    {
        WorkflowDocument workflow = Workflow(node: new WorkflowNode("request", "interaction.request", 1, parameters: new JsonObject
        {
            ["timeout"] = "PT5M",
            ["default"] = null,
            ["options"] = new JsonArray(new JsonObject { ["id"] = "a", ["label"] = "A" }, new JsonObject { ["id"] = "b", ["label"] = "B" }),
            ["prompt"] = "Choose",
            ["kind"] = "choice",
            ["required"] = true,
        }));

        string json = RoundTrip(workflow);

        AssertInOrder(json, "\"kind\"", "\"prompt\"", "\"options\"", "\"default\"", "\"required\"", "\"timeout\"");
        AssertInOrder(json, "\"id\": \"a\"", "\"id\": \"b\"");
        Assert.Contains("\"default\": null", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies duplicate and unknown resource properties are rejected.
    /// </summary>
    [Fact]
    public void RejectsDuplicateAndUnknownResourceProperties()
    {
        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(Minimal("""
              "resources": { "browser": { "kind": "web.browser", "kind": "web.page" } },
            """)));

        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(Minimal("""
              "resources": { "browser": { "kind": "web.browser", "unknown": true } },
            """)));
    }

    /// <summary>
    /// Verifies canonical root property order includes resources.
    /// </summary>
    [Fact]
    public void SerializesNewRootPropertyOrderCanonically()
    {
        string json = _serializer.Serialize(Workflow(), indented: false);

        AssertInOrder(json, "\"$schema\"", "\"specVersion\"", "\"id\"", "\"name\"", "\"inputs\"", "\"variables\"", "\"resources\"", "\"nodes\"", "\"connections\"", "\"outputs\"");
    }

    private string RoundTrip(WorkflowDocument workflow)
    {
        string first = _serializer.Serialize(workflow);
        string second = _serializer.Serialize(_serializer.Deserialize(first));
        Assert.Equal(first, second);
        return second;
    }

    private static WorkflowDocument Workflow(
        IReadOnlyDictionary<string, WorkflowResourceDefinition>? resources = null,
        WorkflowNode? node = null)
    {
        return new WorkflowDocument(
            id: "workflow",
            name: "Workflow",
            resources: resources ?? new Dictionary<string, WorkflowResourceDefinition> { ["browser"] = new WorkflowResourceDefinition(StandardWorkflowResourceKinds.WebBrowser) },
            nodes: [node ?? new WorkflowNode("start", "core.start", 1)],
            connections: []);
    }

    private static JsonObject Resource(string name)
    {
        return new JsonObject { ["$resource"] = new JsonObject { ["name"] = name } };
    }

    private static JsonObject Locator(string catalog, string version, string id)
    {
        return new JsonObject { ["$locator"] = new JsonObject { ["catalog"] = catalog, ["version"] = version, ["id"] = id } };
    }

    private static string Minimal(string middle)
    {
        return $$"""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "workflow",
              "name": "Workflow",
            {{middle}}
              "nodes": [{ "id": "start", "type": "core.start", "typeVersion": 1 }],
              "connections": []
            }
            """;
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
