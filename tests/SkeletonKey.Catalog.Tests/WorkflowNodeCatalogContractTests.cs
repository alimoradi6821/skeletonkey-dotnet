using System.Text.Json.Nodes;
using SkeletonKey.Catalog;

namespace SkeletonKey.Catalog.Tests;

/// <summary>
/// Covers host-neutral node catalog contracts.
/// </summary>
public sealed class WorkflowNodeCatalogContractTests
{
    /// <summary>
    /// Verifies node definitions defensively copy mutable catalog metadata.
    /// </summary>
    [Fact]
    public void NodeDefinitionDefensivelyCopiesMutableMetadata()
    {
        JsonObject schema = new()
        {
            ["type"] = "object",
        };
        Dictionary<string, WorkflowPortDefinition> outputs = new()
        {
            ["main"] = new("main", WorkflowPortDirection.Output),
        };
        List<string> capabilities = ["logging.write"];

        WorkflowNodeDefinition definition = new(
            "core.log",
            1,
            parametersSchema: schema,
            outputs: outputs,
            capabilities: capabilities);

        schema["type"] = "array";
        outputs["other"] = new("other", WorkflowPortDirection.Output);
        capabilities.Add("extra");
        definition.ParametersSchema!["type"] = "string";

        Assert.Equal("object", definition.ParametersSchema!["type"]!.GetValue<string>());
        Assert.Single(definition.Outputs);
        Assert.Equal(["logging.write"], definition.Capabilities);
    }

    /// <summary>
    /// Verifies node definitions defensively clone example parameters.
    /// </summary>
    [Fact]
    public void NodeDefinitionDefensivelyClonesParameterExamples()
    {
        JsonObject example = new()
        {
            ["message"] = "hello",
        };

        WorkflowNodeDefinition definition = new("core.log", 1, parameterExamples: [example]);
        example["message"] = "changed";
        definition.ParameterExamples[0]["message"] = "mutated";

        Assert.Equal("hello", definition.ParameterExamples[0]["message"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies port definitions defensively clone schema fragments.
    /// </summary>
    [Fact]
    public void PortDefinitionDefensivelyClonesSchema()
    {
        JsonObject schema = new()
        {
            ["type"] = "string",
        };

        WorkflowPortDefinition port = new("value", WorkflowPortDirection.Input, schema: schema);
        schema["type"] = "number";
        port.Schema!["type"] = "boolean";

        Assert.Equal("string", port.Schema!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies in-memory catalogs resolve exact type and version pairs.
    /// </summary>
    [Fact]
    public void CatalogResolvesExactTypeAndVersion()
    {
        WorkflowNodeDefinition v1 = new("core.log", 1);
        WorkflowNodeDefinition v2 = new("core.log", 2);
        WorkflowNodeDefinitionCatalog catalog = new([v1, v2]);

        bool found = catalog.TryGetDefinition("core.log", 2, out WorkflowNodeDefinition? definition);

        Assert.True(found);
        Assert.Same(v2, definition);
        Assert.Equal([v1, v2], catalog.GetDefinitions("core.log"));
    }

    /// <summary>
    /// Verifies catalog lookup is case-sensitive and never selects an implicit latest version.
    /// </summary>
    [Fact]
    public void CatalogLookupIsCaseSensitiveAndDoesNotSelectLatestVersion()
    {
        WorkflowNodeDefinition v1 = new("core.log", 1);
        WorkflowNodeDefinition v2 = new("core.log", 2);
        WorkflowNodeDefinitionCatalog catalog = new([v2, v1]);

        Assert.False(catalog.TryGetDefinition("Core.Log", 1, out _));
        Assert.False(catalog.TryGetDefinition("core.log", 3, out _));
        Assert.Equal([v1, v2], catalog.GetDefinitions("core.log"));
        Assert.Equal([v2, v1], catalog.Definitions);
    }

    /// <summary>
    /// Verifies immutable catalog lookup can be read concurrently.
    /// </summary>
    [Fact]
    public void CatalogLookupCanBeReadConcurrently()
    {
        WorkflowNodeDefinition definition = new("core.log", 1);
        WorkflowNodeDefinitionCatalog catalog = new([definition]);

        Parallel.For(
            0,
            100,
            _ =>
            {
                Assert.True(catalog.TryGetDefinition("core.log", 1, out WorkflowNodeDefinition? found));
                Assert.Same(definition, found);
            });
    }

    /// <summary>
    /// Verifies duplicate type and version definitions are rejected.
    /// </summary>
    [Fact]
    public void CatalogRejectsDuplicateTypeAndVersion()
    {
        WorkflowNodeDefinition first = new("core.log", 1);
        WorkflowNodeDefinition second = new("core.log", 1);

        Assert.Throws<ArgumentException>(() => new WorkflowNodeDefinitionCatalog([first, second]));
    }
}
