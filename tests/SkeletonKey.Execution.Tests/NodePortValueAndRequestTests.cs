using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Execution.Tests;

/// <summary>
/// Verifies node port values and node execution request immutability contracts.
/// </summary>
public sealed class NodePortValueAndRequestTests
{
    /// <summary>
    /// Verifies an empty value set represents no supplied values.
    /// </summary>
    [Fact]
    public void EmptyValueSetRepresentsNoValues()
    {
        NodePortValueSet set = new();

        Assert.Empty(set.Values);
    }

    /// <summary>
    /// Verifies one null item represents an explicit JSON null value.
    /// </summary>
    [Fact]
    public void OneNullItemRepresentsExplicitJsonNull()
    {
        NodePortValueSet set = new([null]);

        Assert.Single(set.Values);
        Assert.Null(set.Values[0]);
    }

    /// <summary>
    /// Verifies multiple values preserve order and defensive cloning.
    /// </summary>
    [Fact]
    public void MultipleValuesPreserveOrderAndCloneJson()
    {
        JsonObject first = new() { ["value"] = 1 };
        NodePortValueSet set = new([first, JsonValue.Create("second")]);

        first["value"] = 99;
        IReadOnlyList<JsonNode?> values = set.Values;
        Assert.Equal(1, values[0]!["value"]!.GetValue<int>());
        Assert.Equal("second", values[1]!.GetValue<string>());

        values[0]!["value"] = 100;
        Assert.Equal(1, set.Values[0]!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies port maps preserve order, immutability, and case-sensitive port IDs.
    /// </summary>
    [Fact]
    public void PortMapsPreserveOrderAndCaseSensitiveKeys()
    {
        Dictionary<string, NodePortValueSet> values = new(StringComparer.Ordinal)
        {
            ["result"] = new([JsonValue.Create(1)]),
            ["Result"] = new([JsonValue.Create(2)]),
        };

        NodePortValueMap map = new(values);
        values["late"] = new();

        Assert.Equal(["result", "Result"], map.Values.Keys);
        Assert.Equal(1, map.Values["result"].Values[0]!.GetValue<int>());
        Assert.Equal(2, map.Values["Result"].Values[0]!.GetValue<int>());
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, NodePortValueSet>>(map.Values);
    }

    /// <summary>
    /// Verifies requests clone parameters and preserve activated control input order.
    /// </summary>
    [Fact]
    public void RequestClonesParametersAndPreservesControlInputOrder()
    {
        JsonObject parameters = new() { ["message"] = "hello" };
        NodeExecutionRequest request = new(Identity(), parameters, ["main", "retry"]);

        parameters["message"] = "changed";
        Assert.Equal("hello", request.Parameters["message"]!.GetValue<string>());
        Assert.Equal(["main", "retry"], request.ActivatedControlInputs);

        JsonObject returned = request.Parameters;
        returned["message"] = "mutated";
        Assert.Equal("hello", request.Parameters["message"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies duplicate activated control inputs are rejected.
    /// </summary>
    [Fact]
    public void RequestRejectsDuplicateControlInputs()
    {
        Assert.Throws<ArgumentException>(() => new NodeExecutionRequest(Identity(), activatedControlInputs: ["main", "main"]));
    }

    /// <summary>
    /// Verifies requests defensively copy data inputs and iteration contexts.
    /// </summary>
    [Fact]
    public void RequestDefensivelyCopiesDataInputsAndIterations()
    {
        Dictionary<string, NodePortValueSet> dataInputs = new(StringComparer.Ordinal)
        {
            ["input"] = new([JsonValue.Create("value")]),
        };
        Dictionary<string, WorkflowIterationContext> iterations = new(StringComparer.Ordinal)
        {
            ["loop"] = new("loop", 0, 1, JsonValue.Create("item"), hasItem: true, count: 2),
        };

        NodeExecutionRequest request = new(Identity(), dataInputs: dataInputs, iterations: iterations);
        dataInputs["late"] = new();
        iterations["late"] = new("late", 1, 2);

        Assert.Equal(["input"], request.DataInputs.Keys);
        Assert.Equal(["loop"], request.Iterations.Keys);
        Assert.Equal("item", request.Iterations["loop"].Item!.GetValue<string>());
    }

    /// <summary>
    /// Verifies request collections expose no mutable workflow state.
    /// </summary>
    [Fact]
    public void RequestCollectionsExposeNoMutableWorkflowState()
    {
        NodeExecutionRequest request = new(Identity(), activatedControlInputs: ["main"]);

        Assert.IsNotType<List<string>>(request.ActivatedControlInputs);
        Assert.IsNotType<Dictionary<string, NodePortValueSet>>(request.DataInputs);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, WorkflowIterationContext>>(request.Iterations);
    }

    private static NodeExecutionIdentity Identity()
    {
        return new NodeExecutionIdentity("execution", "invocation", null, "workflow", "node", new("core.log", 1), "plan", "step", 1);
    }
}
