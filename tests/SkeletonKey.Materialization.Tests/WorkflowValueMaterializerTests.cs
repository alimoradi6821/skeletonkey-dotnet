using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Evaluation;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.Materialization.Tests;

/// <summary>
/// Verifies recursive workflow-value and node parameter materialization.
/// </summary>
public sealed class WorkflowValueMaterializerTests
{
    private readonly WorkflowValueMaterializer _materializer = new();

    /// <summary>
    /// Materializes scalars, arrays, objects, nested bindings, and nested expressions in deterministic order.
    /// </summary>
    [Fact]
    public void MaterializesWorkflowValuesRecursively()
    {
        JsonObject source = new()
        {
            ["recipient"] = Binding("iteration", iteration: "process-contacts", path: "/item/name"),
            ["message"] = Expression("trim(variables.message)"),
            ["attemptNumber"] = Expression("iterations['process-contacts'].number"),
            ["tags"] = new JsonArray(Binding("input", name: "tag"), Expression("upper('ok')")),
        };

        WorkflowValueResult result = _materializer.Materialize(source, Context());

        Assert.True(result.IsSuccess, result.Error?.Message);
        JsonObject value = result.Value!.AsObject();
        Assert.Equal(["recipient", "message", "attemptNumber", "tags"], value.Select(static property => property.Key));
        Assert.Equal("Ada", value["recipient"]!.GetValue<string>());
        Assert.Equal("Hello", value["message"]!.GetValue<string>());
        Assert.Equal(1, value["attemptNumber"]!.GetValue<decimal>());
        Assert.Equal("blue", value["tags"]![0]!.GetValue<string>());
        Assert.Equal("OK", value["tags"]![1]!.GetValue<string>());
    }

    /// <summary>
    /// Unwraps literal wrappers without recursing into inner workflow-value wrappers and preserves explicit null.
    /// </summary>
    [Fact]
    public void LiteralWrapperEscapesRecursiveInterpretation()
    {
        JsonObject literalBinding = new()
        {
            ["$literal"] = Binding("input", name: "tag"),
        };
        JsonObject literalNull = new()
        {
            ["$literal"] = null,
        };

        WorkflowValueResult bindingResult = _materializer.Materialize(literalBinding, Context());
        WorkflowValueResult nullResult = _materializer.Materialize(literalNull, Context());

        Assert.True(bindingResult.Value!.AsObject().ContainsKey("$binding"));
        Assert.Null(nullResult.Value);
    }

    /// <summary>
    /// Rejects malformed reserved wrappers and generic resource or locator materialization.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidWrappers))]
    public void RejectsMalformedResourceAndLocatorWrappers(JsonObject wrapper, string expectedCode)
    {
        WorkflowValueResult result = _materializer.Materialize(wrapper, Context());

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    /// <summary>
    /// Enforces materialization depth, collection size, string length, and defensive JSON ownership.
    /// </summary>
    [Fact]
    public void EnforcesLimitsAndDefensiveCloning()
    {
        JsonObject source = new() { ["value"] = "abc" };
        WorkflowValueResult result = _materializer.Materialize(source, Context());
        JsonObject materialized = result.Value!.AsObject();
        materialized["value"] = "changed";

        Assert.Equal("abc", _materializer.Materialize(source, Context()).Value!["value"]!.GetValue<string>());
        Assert.Equal(WorkflowValueErrorCode.MaterializationDepthLimitExceeded, _materializer.Materialize(new JsonArray(new JsonArray(1)), Context(), new WorkflowValueProcessingLimits(maximumMaterializationDepth: 1)).Error!.Code);
        Assert.Equal(WorkflowValueErrorCode.ResultSizeLimitExceeded, _materializer.Materialize(new JsonArray(1, 2), Context(), new WorkflowValueProcessingLimits(maximumCollectionItems: 1)).Error!.Code);
        Assert.Equal(WorkflowValueErrorCode.ResultSizeLimitExceeded, _materializer.Materialize("abcd", Context(), new WorkflowValueProcessingLimits(maximumStringLength: 3)).Error!.Code);
    }

    /// <summary>
    /// Repeated and parallel materialization is deterministic and thread-safe.
    /// </summary>
    [Fact]
    public void MaterializationIsDeterministicAndThreadSafe()
    {
        JsonObject source = new() { ["message"] = Expression("trim(variables.message)") };

        WorkflowValueResult[] results = ParallelEnumerable.Range(0, 64)
            .Select(_ => _materializer.Materialize(source, Context()))
            .ToArray();

        Assert.All(results, result => Assert.Equal("Hello", result.Value!["message"]!.GetValue<string>()));
    }

    /// <summary>
    /// Materialized node parameters are plain JSON and can be supplied to a future node execution request without executing a handler.
    /// </summary>
    [Fact]
    public void MaterializedParametersCanBeSuppliedToNodeExecutionRequest()
    {
        JsonObject parameters = new()
        {
            ["recipient"] = Binding("iteration", iteration: "process-contacts", path: "/item"),
            ["message"] = Expression("trim(variables.message)"),
            ["attemptNumber"] = Expression("iterations['process-contacts'].number"),
        };

        WorkflowValueResult result = new NodeParameterMaterializer().MaterializeParameters(parameters, Context());

        Assert.True(result.IsSuccess, result.Error?.Message);
        JsonObject materialized = result.Value!.AsObject();
        Assert.False(ContainsReserved(materialized, "$binding"));
        Assert.False(ContainsReserved(materialized, "$expression"));

        NodeExecutionRequest request = new(
            new NodeExecutionIdentity("execution", "invocation", null, "workflow", "node", new WorkflowNodeDefinitionKey("custom.send", 1), "plan", "step", 1),
            materialized);

        Assert.Equal("Hello", request.Parameters["message"]!.GetValue<string>());
        Assert.Equal(1, request.Parameters["attemptNumber"]!.GetValue<decimal>());
        Assert.False(typeof(INodeHandler).IsAssignableFrom(typeof(NodeParameterMaterializer)));
    }

    /// <summary>
    /// Provides invalid wrapper test data.
    /// </summary>
    public static TheoryData<JsonObject, string> InvalidWrappers()
    {
        return new TheoryData<JsonObject, string>
        {
            { new JsonObject { ["$binding"] = new JsonObject(), ["extra"] = true }, WorkflowValueErrorCode.MalformedWorkflowValueWrapper },
            { new JsonObject { ["$expression"] = "true", ["$literal"] = false }, WorkflowValueErrorCode.MalformedWorkflowValueWrapper },
            { new JsonObject { ["$literal"] = true, ["extra"] = false }, WorkflowValueErrorCode.MalformedWorkflowValueWrapper },
            { new JsonObject { ["$resource"] = new JsonObject { ["name"] = "browser" } }, WorkflowValueErrorCode.ResourceReferenceCannotBeJsonMaterialized },
            { new JsonObject { ["$locator"] = new JsonObject { ["catalog"] = "main", ["id"] = "save" } }, WorkflowValueErrorCode.LocatorReferenceCannotBeJsonMaterialized },
        };
    }

    private static WorkflowValueResolutionContext Context()
    {
        return new WorkflowValueResolutionContext(
            new Dictionary<string, JsonNode?> { ["tag"] = "blue" },
            new Dictionary<string, JsonNode?> { ["message"] = " Hello " },
            new Dictionary<string, NodePortValueMap>
            {
                ["prior"] = new(new Dictionary<string, NodePortValueSet> { ["result"] = new([JsonValue.Create("ok")]) }),
            },
            new Dictionary<string, WorkflowIterationContext>
            {
                ["process-contacts"] = new("process-contacts", 0, 1, new JsonObject { ["name"] = "Ada" }, hasItem: true, count: 1),
            });
    }

    private static JsonObject Binding(string source, string? name = null, string? iteration = null, string path = "")
    {
        JsonObject binding = new()
        {
            ["source"] = source,
            ["path"] = path,
        };

        if (name is not null)
        {
            binding["name"] = name;
        }

        if (iteration is not null)
        {
            binding["iteration"] = iteration;
        }

        return new JsonObject { ["$binding"] = binding };
    }

    private static JsonObject Expression(string expression)
    {
        return new JsonObject { ["$expression"] = expression };
    }

    private static bool ContainsReserved(JsonNode? value, string reservedName)
    {
        if (value is JsonObject jsonObject)
        {
            return jsonObject.ContainsKey(reservedName) || jsonObject.Any(property => ContainsReserved(property.Value, reservedName));
        }

        if (value is JsonArray array)
        {
            return array.Any(item => ContainsReserved(item, reservedName));
        }

        return false;
    }
}
