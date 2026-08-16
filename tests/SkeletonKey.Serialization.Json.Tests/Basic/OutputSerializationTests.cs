using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;

namespace SkeletonKey.Serialization.Json.Tests.Basic;

/// <summary>
/// Covers workflow output JSON serialization behavior.
/// </summary>
public sealed class OutputSerializationTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies omitted outputs deserialize to an empty dictionary.
    /// </summary>
    [Fact]
    public void DeserializesWorkflowWithoutOutputsToEmptyOutputs()
    {
        WorkflowDocument workflow = _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": []
            }
            """);

        Assert.Empty(workflow.Outputs);
    }

    /// <summary>
    /// Verifies single, collection, and stream outputs deserialize from JSON.
    /// </summary>
    [Fact]
    public void DeserializesAllOutputModes()
    {
        WorkflowDocument workflow = _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "outputs",
              "name": "Outputs",
              "nodes": [],
              "connections": [],
              "outputs": {
                "single": { "mode": "single", "from": { "node": "node", "port": "main" } },
                "items": { "mode": "collection", "from": { "node": "node", "port": "items" } },
                "events": { "mode": "stream", "channel": "events" }
              }
            }
            """);

        Assert.Equal(WorkflowOutputMode.Single, workflow.Outputs["single"].Mode);
        Assert.Equal(new WorkflowEndpoint("node", "items"), workflow.Outputs["items"].From);
        Assert.Equal("events", workflow.Outputs["events"].Channel);
    }

    /// <summary>
    /// Verifies outputs appear in the canonical root order.
    /// </summary>
    [Fact]
    public void SerializesOutputsInCanonicalRootOrder()
    {
        string json = _serializer.Serialize(CreateOutputWorkflow(), indented: false);

        AssertInOrder(json, "\"connections\"", "\"outputs\"", "\"designer\"");
    }

    /// <summary>
    /// Verifies output definition properties appear in canonical order.
    /// </summary>
    [Fact]
    public void SerializesOutputDefinitionsInCanonicalPropertyOrder()
    {
        string json = _serializer.Serialize(CreateOutputWorkflow(), indented: false);

        AssertInOrder(json, "\"result\"", "\"mode\"", "\"from\"", "\"channel\"");
        AssertInOrder(json, "\"stream\"", "\"mode\":\"stream\"", "\"channel\":\"records\"", "\"description\"");
    }

    /// <summary>
    /// Verifies output modes serialize as lowercase language strings.
    /// </summary>
    [Fact]
    public void SerializesOutputModesAsLowercaseStrings()
    {
        string json = _serializer.Serialize(CreateOutputWorkflow(), indented: false);

        Assert.Contains("\"mode\":\"single\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"collection\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"stream\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies numeric output modes are rejected.
    /// </summary>
    [Fact]
    public void RejectsNumericOutputMode()
    {
        AssertInvalidOutput("""{ "mode": 1, "from": { "node": "node", "port": "main" } }""");
    }

    /// <summary>
    /// Verifies unknown output modes are rejected.
    /// </summary>
    [Fact]
    public void RejectsUnknownOutputMode()
    {
        AssertInvalidOutput("""{ "mode": "value", "from": { "node": "node", "port": "main" } }""");
    }

    /// <summary>
    /// Verifies unknown output properties are rejected.
    /// </summary>
    [Fact]
    public void RejectsUnknownOutputProperty()
    {
        AssertInvalidOutput("""{ "mode": "single", "from": { "node": "node", "port": "main" }, "extra": true }""");
    }

    /// <summary>
    /// Verifies null output dictionaries are rejected.
    /// </summary>
    [Fact]
    public void RejectsNullOutputsDictionary()
    {
        AssertInvalidWorkflowWithOutputs("null");
    }

    /// <summary>
    /// Verifies null output definitions are rejected.
    /// </summary>
    [Fact]
    public void RejectsNullOutputDefinition()
    {
        AssertInvalidWorkflowWithOutputs("""{ "result": null }""");
    }

    /// <summary>
    /// Verifies null output sources are rejected.
    /// </summary>
    [Fact]
    public void RejectsNullOutputSource()
    {
        AssertInvalidOutput("""{ "mode": "single", "from": null }""");
    }

    /// <summary>
    /// Verifies null stream channels are rejected.
    /// </summary>
    [Fact]
    public void RejectsNullStreamChannel()
    {
        AssertInvalidOutput("""{ "mode": "stream", "channel": null }""");
    }

    /// <summary>
    /// Verifies all output modes round-trip through canonical JSON.
    /// </summary>
    [Fact]
    public void RoundTripsAllOutputModes()
    {
        string json = _serializer.Serialize(CreateOutputWorkflow());
        string roundTripped = _serializer.Serialize(_serializer.Deserialize(json));

        Assert.Equal(json, roundTripped);
    }

    /// <summary>
    /// Verifies output dictionary order is preserved.
    /// </summary>
    [Fact]
    public void PreservesOutputDictionaryOrder()
    {
        string json = _serializer.Serialize(CreateOutputWorkflow(), indented: false);

        AssertInOrder(json, "\"result\"", "\"items\"", "\"stream\"");
    }

    private static WorkflowDocument CreateOutputWorkflow()
    {
        return new WorkflowDocument(
            id: "outputs",
            name: "Outputs",
            nodes:
            [
                new WorkflowNode("start", "core.start", 1),
                new WorkflowNode("log", "core.log", 1),
            ],
            connections:
            [
                new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("log", "main")),
            ],
            outputs: new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("log", "main"), channel: "unexpected"),
                ["items"] = new WorkflowOutputDefinition(WorkflowOutputMode.Collection, new WorkflowEndpoint("log", "items")),
                ["stream"] = new WorkflowOutputDefinition(WorkflowOutputMode.Stream, channel: "records", description: "Records."),
            },
            designer: new SkeletonKey.Workflow.Designer.WorkflowDesignerMetadata());
    }

    private void AssertInvalidOutput(string output)
    {
        AssertInvalidWorkflowWithOutputs($$"""{ "result": {{output}} }""");
    }

    private void AssertInvalidWorkflowWithOutputs(string outputs)
    {
        string json = $$"""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "outputs",
              "name": "Outputs",
              "nodes": [],
              "connections": [],
              "outputs": {{outputs}}
            }
            """;

        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(json));
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
