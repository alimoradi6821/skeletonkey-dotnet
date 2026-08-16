using System.Text.Json.Nodes;

namespace SkeletonKey.Expressions.Tests;

/// <summary>
/// Covers expression workflow-value wrapper inspection.
/// </summary>
public sealed class WorkflowExpressionReaderTests
{
    private readonly WorkflowExpressionReader _reader = new();

    /// <summary>
    /// Recognizes a valid expression wrapper.
    /// </summary>
    [Fact]
    public void IsExpressionRecognizesExpressionWrapper()
    {
        JsonNode value = JsonNode.Parse("""{"$expression":"size(inputs.items) > 0"}""")!;

        Assert.True(_reader.IsExpression(value));
        Assert.Equal("size(inputs.items) > 0", _reader.ReadText(value));
    }

    /// <summary>
    /// Rejects expression wrappers with sibling properties.
    /// </summary>
    [Fact]
    public void ReadTextRejectsExpressionWrapperWithSiblings()
    {
        JsonNode value = JsonNode.Parse("""{"$expression":"true","other":1}""")!;

        Assert.Throws<WorkflowExpressionFormatException>(() => _reader.ReadText(value));
    }

    /// <summary>
    /// Does not inspect expressions inside literal wrappers.
    /// </summary>
    [Fact]
    public void FindExpressionsDoesNotInspectInsideLiteralWrapper()
    {
        JsonNode value = JsonNode.Parse("""{"$literal":{"$expression":"not parsed"}}""")!;

        Assert.Empty(_reader.FindExpressions(value));
    }

    /// <summary>
    /// Reports nested expression wrapper paths using JSON Pointer.
    /// </summary>
    [Fact]
    public void FindExpressionsReportsJsonPointerLocations()
    {
        JsonNode value = JsonNode.Parse("""{"items":[{"condition":{"$expression":"inputs.enabled"}}]}""")!;

        WorkflowExpressionOccurrence occurrence = Assert.Single(_reader.FindExpressions(value));

        Assert.Equal("/items/0/condition", occurrence.Path);
        Assert.Equal("inputs.enabled", occurrence.Text);
    }

    /// <summary>
    /// Reader inspection does not mutate the source JSON.
    /// </summary>
    [Fact]
    public void FindExpressionsDoesNotMutateJson()
    {
        JsonNode value = JsonNode.Parse("""{"z":{"$expression":"inputs.enabled"}}""")!;
        string before = value.ToJsonString();

        _ = _reader.FindExpressions(value);

        Assert.Equal(before, value.ToJsonString());
    }
}
