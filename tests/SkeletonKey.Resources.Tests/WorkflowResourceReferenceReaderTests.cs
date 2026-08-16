using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Resources.Tests;

/// <summary>
/// Covers resource reference inspection behavior.
/// </summary>
public sealed class WorkflowResourceReferenceReaderTests
{
    private readonly WorkflowResourceReferenceReader _reader = new();

    /// <summary>
    /// Verifies valid wrappers are recognized and read.
    /// </summary>
    [Fact]
    public void RecognizesValidResourceWrapper()
    {
        JsonObject value = Resource("browser");

        WorkflowResourceReference reference = _reader.Read(value);

        Assert.True(_reader.IsResourceReference(value));
        Assert.Equal("browser", reference.Name);
    }

    /// <summary>
    /// Verifies wrappers with sibling properties are rejected.
    /// </summary>
    [Fact]
    public void RejectsResourceWrapperWithSiblings()
    {
        JsonObject value = Resource("browser");
        value["extra"] = true;

        Assert.Throws<WorkflowResourceReferenceFormatException>(() => _reader.Read(value));
    }

    /// <summary>
    /// Verifies malformed wrappers are rejected.
    /// </summary>
    [Theory]
    [InlineData("{\"$resource\":null}")]
    [InlineData("{\"$resource\":{}}")]
    [InlineData("{\"$resource\":{\"name\":1}}")]
    [InlineData("{\"$resource\":{\"name\":\"bad name\"}}")]
    public void RejectsMalformedResourceWrapper(string json)
    {
        Assert.Throws<WorkflowResourceReferenceFormatException>(() => _reader.Read(JsonNode.Parse(json)!));
    }

    /// <summary>
    /// Verifies nested references are found in deterministic order with paths.
    /// </summary>
    [Fact]
    public void FindsNestedResourceReferencesAndReportsPaths()
    {
        JsonObject value = new()
        {
            ["outer"] = new JsonArray(Resource("browser"), new JsonObject { ["child"] = Resource("page") }),
        };

        IReadOnlyList<WorkflowResourceReferenceOccurrence> occurrences = _reader.FindResourceReferences(value);

        Assert.Equal(["/outer/0", "/outer/1/child"], [.. occurrences.Select(static occurrence => occurrence.Path)]);
        Assert.Equal(["browser", "page"], [.. occurrences.Select(static occurrence => occurrence.Reference.Name)]);
    }

    /// <summary>
    /// Verifies object keys are escaped in occurrence paths.
    /// </summary>
    [Fact]
    public void EscapesObjectKeysInOccurrencePaths()
    {
        JsonObject value = new()
        {
            ["a/b~c"] = Resource("browser"),
        };

        WorkflowResourceReferenceOccurrence occurrence = Assert.Single(_reader.FindResourceReferences(value));

        Assert.Equal("/a~1b~0c", occurrence.Path);
    }

    /// <summary>
    /// Verifies literal wrappers stop inspection.
    /// </summary>
    [Fact]
    public void DoesNotInspectInsideLiteralWrapper()
    {
        JsonObject value = new()
        {
            ["literal"] = new JsonObject
            {
                ["$literal"] = Resource("browser"),
            },
        };

        Assert.Empty(_reader.FindResourceReferences(value));
    }

    /// <summary>
    /// Verifies scanning does not mutate source JSON.
    /// </summary>
    [Fact]
    public void DoesNotMutateSourceJson()
    {
        JsonObject value = new() { ["resource"] = Resource("browser") };
        string before = value.ToJsonString();

        _ = _reader.FindResourceReferences(value);

        Assert.Equal(before, value.ToJsonString());
    }

    /// <summary>
    /// Verifies repeated and concurrent scans are deterministic.
    /// </summary>
    [Fact]
    public async Task RepeatedScansAreDeterministicAndThreadSafe()
    {
        JsonObject value = new()
        {
            ["one"] = Resource("browser"),
            ["two"] = Resource("page"),
        };

        string[] first = [.. _reader.FindResourceReferences(value).Select(static occurrence => occurrence.Reference.Name)];
        string[][] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            _reader.FindResourceReferences(value).Select(static occurrence => occurrence.Reference.Name).ToArray())));

        Assert.All(results, result => Assert.Equal(first, result));
    }

    private static JsonObject Resource(string name)
    {
        return new JsonObject { ["$resource"] = new JsonObject { ["name"] = name } };
    }
}
