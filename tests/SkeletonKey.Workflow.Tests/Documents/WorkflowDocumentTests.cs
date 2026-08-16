using System.Collections;
using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Designer;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;

namespace SkeletonKey.Workflow.Tests.Documents;

/// <summary>
/// Covers root workflow document construction behavior.
/// </summary>
public sealed class WorkflowDocumentTests
{
    /// <summary>
    /// Verifies omitted optional collections become empty collections.
    /// </summary>
    [Fact]
    public void UsesEmptyCollections_WhenOptionalCollectionsAreOmitted()
    {
        WorkflowDocument document = new();

        Assert.Empty(document.Inputs);
        Assert.Empty(document.Variables);
        Assert.Empty(document.Nodes);
        Assert.Empty(document.Connections);
        Assert.Empty(document.Outputs);
    }

    /// <summary>
    /// Verifies explicit schema and specification values are preserved.
    /// </summary>
    [Fact]
    public void PreservesExplicitSchemaAndSpecificationVersion()
    {
        WorkflowDocument document = new(
            schema: "https://example.invalid/schema.json",
            specVersion: "test-version");

        Assert.Equal("https://example.invalid/schema.json", document.Schema);
        Assert.Equal("test-version", document.SpecVersion);
    }

    /// <summary>
    /// Verifies caller mutations to source collections do not affect the document.
    /// </summary>
    [Fact]
    public void DefensivelyCopiesInputCollections()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["name"] = new WorkflowInputDefinition(WorkflowInputType.String),
        };
        JsonObject sourceVariable = new()
        {
            ["count"] = 1,
        };
        Dictionary<string, JsonNode?> variables = new()
        {
            ["state"] = sourceVariable,
        };
        Dictionary<string, WorkflowOutputDefinition> outputs = new()
        {
            ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("start", "main")),
        };
        List<WorkflowNode> nodes =
        [
            new WorkflowNode("start", "core.start", 1),
        ];
        List<WorkflowConnection> connections =
        [
            new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("end", "main")),
        ];

        WorkflowDocument document = new(
            inputs: inputs,
            variables: variables,
            outputs: outputs,
            nodes: nodes,
            connections: connections);

        inputs["other"] = new WorkflowInputDefinition(WorkflowInputType.Boolean);
        sourceVariable["count"] = 2;
        variables["new"] = JsonValue.Create(true);
        outputs["other"] = new WorkflowOutputDefinition(WorkflowOutputMode.Stream, channel: "events");
        nodes.Add(new WorkflowNode("end", "core.end", 1));
        connections.Add(new WorkflowConnection(new WorkflowEndpoint("end", "main"), new WorkflowEndpoint("start", "main")));

        Assert.Single(document.Inputs);
        Assert.Single(document.Variables);
        Assert.Equal(1, document.Variables["state"]!["count"]!.GetValue<int>());
        Assert.Single(document.Outputs);
        Assert.Single(document.Nodes);
        Assert.Single(document.Connections);
    }

    /// <summary>
    /// Verifies the node collection is not exposed as a mutable list.
    /// </summary>
    [Fact]
    public void DoesNotExposeMutableNodeCollections()
    {
        WorkflowDocument document = new(
            nodes:
            [
                new WorkflowNode("start", "core.start", 1),
            ]);

        Assert.IsNotType<List<WorkflowNode>>(document.Nodes);
        Assert.Throws<NotSupportedException>(() => ((IList<WorkflowNode>)document.Nodes).Add(new WorkflowNode("end", "core.end", 1)));
    }

    /// <summary>
    /// Verifies the connection collection is not exposed as a mutable list.
    /// </summary>
    [Fact]
    public void DoesNotExposeMutableConnectionCollections()
    {
        WorkflowDocument document = new(
            connections:
            [
                new WorkflowConnection(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("end", "main")),
            ]);

        Assert.IsNotType<List<WorkflowConnection>>(document.Connections);
        Assert.Throws<NotSupportedException>(() => ((IList<WorkflowConnection>)document.Connections).Add(default));
    }

    /// <summary>
    /// Verifies the input dictionary is not exposed as a mutable dictionary.
    /// </summary>
    [Fact]
    public void DoesNotExposeMutableInputDictionaries()
    {
        WorkflowDocument document = new(
            inputs: new Dictionary<string, WorkflowInputDefinition>
            {
                ["name"] = new WorkflowInputDefinition(WorkflowInputType.String),
            });

        Assert.IsNotType<Dictionary<string, WorkflowInputDefinition>>(document.Inputs);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, WorkflowInputDefinition>)document.Inputs).Add("other", new WorkflowInputDefinition(WorkflowInputType.Boolean)));
    }

    /// <summary>
    /// Verifies variable dictionaries and JSON values are returned defensively.
    /// </summary>
    [Fact]
    public void DoesNotExposeMutableVariableDictionaries()
    {
        WorkflowDocument document = new(
            variables: new Dictionary<string, JsonNode?>
            {
                ["state"] = new JsonObject
                {
                    ["count"] = 1,
                },
            });

        IReadOnlyDictionary<string, JsonNode?> variables = document.Variables;
        Assert.IsNotType<Dictionary<string, JsonNode?>>(variables);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, JsonNode?>)variables).Add("other", JsonValue.Create(true)));

        variables["state"]!["count"] = 2;

        Assert.Equal(1, document.Variables["state"]!["count"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies output dictionaries are not exposed as mutable dictionaries.
    /// </summary>
    [Fact]
    public void DoesNotExposeMutableOutputDictionaries()
    {
        WorkflowDocument document = new(
            outputs: new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("start", "main")),
            });

        Assert.IsNotType<Dictionary<string, WorkflowOutputDefinition>>(document.Outputs);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, WorkflowOutputDefinition>)document.Outputs).Add(
            "other",
            new WorkflowOutputDefinition(WorkflowOutputMode.Stream, channel: "events")));
    }

    /// <summary>
    /// Verifies optional descriptions are preserved.
    /// </summary>
    [Fact]
    public void PreservesOptionalDescription()
    {
        WorkflowDocument document = new(description: "A useful workflow.");

        Assert.Equal("A useful workflow.", document.Description);
    }

    /// <summary>
    /// Verifies designer metadata may be absent.
    /// </summary>
    [Fact]
    public void AllowsDesignerMetadataToBeAbsent()
    {
        WorkflowDocument document = new();

        Assert.Null(document.Designer);
    }

    /// <summary>
    /// Verifies designer metadata can be attached without changing node data.
    /// </summary>
    [Fact]
    public void PreservesDesignerMetadataWhenProvided()
    {
        WorkflowDesignerMetadata designer = new(
            positions: new Dictionary<string, WorkflowNodePosition>
            {
                ["start"] = new WorkflowNodePosition(10, 20),
            });

        WorkflowDocument document = new(designer: designer);

        Assert.Same(designer, document.Designer);
    }
}
