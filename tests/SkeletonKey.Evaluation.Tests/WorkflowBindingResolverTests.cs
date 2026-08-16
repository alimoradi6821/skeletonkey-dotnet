using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;
using SkeletonKey.Workflow.Bindings;

namespace SkeletonKey.Evaluation.Tests;

/// <summary>
/// Verifies structured binding resolution over immutable value contexts.
/// </summary>
public sealed class WorkflowBindingResolverTests
{
    private readonly WorkflowBindingResolver _resolver = new();

    /// <summary>
    /// Resolves input, variable, node, and iteration sources with nested pointers.
    /// </summary>
    [Fact]
    public void ResolvesAllBindingSources()
    {
        WorkflowValueResolutionContext context = Context();

        Assert.Equal("ada", Resolve(new WorkflowBinding(WorkflowBindingSource.Input, name: "account", path: "/id"), context)!.GetValue<string>());
        Assert.Equal("Hello", Resolve(new WorkflowBinding(WorkflowBindingSource.Variable, name: "message"), context)!.GetValue<string>());
        Assert.Equal("ok", Resolve(new WorkflowBinding(WorkflowBindingSource.Node, node: "check", port: "status"), context)!.GetValue<string>());
        Assert.Equal(2, Resolve(new WorkflowBinding(WorkflowBindingSource.Node, node: "check", port: "many"), context)!.AsArray().Count);
        Assert.Equal(0, Resolve(new WorkflowBinding(WorkflowBindingSource.Iteration, iteration: "loop", path: "/index"), context)!.GetValue<long>());
        Assert.Equal("Ada", Resolve(new WorkflowBinding(WorkflowBindingSource.Iteration, iteration: "loop", path: "/item/name"), context)!.GetValue<string>());
        Assert.Null(Resolve(new WorkflowBinding(WorkflowBindingSource.Iteration, iteration: "null-loop", path: "/item"), context));
    }

    /// <summary>
    /// Applies missing-value behavior consistently without hiding malformed pointers.
    /// </summary>
    [Fact]
    public void AppliesMissingBehavior()
    {
        WorkflowValueResolutionContext context = Context();
        WorkflowBinding missingAsNull = new(WorkflowBindingSource.Input, name: "missing", onMissing: WorkflowBindingMissingBehavior.Null);
        WorkflowBinding missingAsDefault = new(WorkflowBindingSource.Input, name: "missing", onMissing: WorkflowBindingMissingBehavior.Default, defaultValue: new JsonObject { ["$binding"] = new JsonObject() }, hasDefault: true);
        WorkflowBinding missingAsExplicitNull = new(WorkflowBindingSource.Input, name: "missing", onMissing: WorkflowBindingMissingBehavior.Default, hasDefault: true);
        WorkflowBinding malformedPointer = new(WorkflowBindingSource.Input, name: "account", path: "/bad~x", onMissing: WorkflowBindingMissingBehavior.Null);

        Assert.Null(_resolver.Resolve(missingAsNull, context, "/p").Value);
        Assert.True(_resolver.Resolve(missingAsDefault, context, "/p").Value!.AsObject().ContainsKey("$binding"));
        Assert.Null(_resolver.Resolve(missingAsExplicitNull, context, "/p").Value);
        Assert.Equal(WorkflowValueErrorCode.InvalidJsonPointer, _resolver.Resolve(malformedPointer, context, "/p").Error!.Code);
    }

    /// <summary>
    /// Treats empty node output sets and absent iteration items as missing.
    /// </summary>
    [Fact]
    public void MissingSourcesReturnStableErrors()
    {
        WorkflowValueResolutionContext context = Context();

        Assert.Equal(WorkflowValueErrorCode.MissingBindingSourceValue, _resolver.Resolve(new WorkflowBinding(WorkflowBindingSource.Node, node: "check", port: "empty"), context, "/p").Error!.Code);
        Assert.Equal(WorkflowValueErrorCode.JsonPointerTargetNotFound, _resolver.Resolve(new WorkflowBinding(WorkflowBindingSource.Iteration, iteration: "no-item", path: "/item"), context, "/p").Error!.Code);
        Assert.Equal(WorkflowValueErrorCode.UnknownNode, _resolver.Resolve(new WorkflowBinding(WorkflowBindingSource.Node, node: "missing", port: "out"), context, "/p").Error!.Code);
    }

    /// <summary>
    /// Repeated and parallel resolution is deterministic and defensively cloned.
    /// </summary>
    [Fact]
    public void ResolutionIsDeterministicThreadSafeAndCloned()
    {
        WorkflowValueResolutionContext context = Context();
        WorkflowBinding binding = new(WorkflowBindingSource.Input, name: "account");

        JsonObject first = _resolver.Resolve(binding, context, "/p").Value!.AsObject();
        first["id"] = "changed";

        Assert.Equal("ada", _resolver.Resolve(binding, context, "/p").Value!["id"]!.GetValue<string>());
        Assert.All(ParallelEnumerable.Range(0, 64).Select(_ => _resolver.Resolve(binding, context, "/p")).ToArray(), result => Assert.Equal("ada", result.Value!["id"]!.GetValue<string>()));
    }

    private static JsonNode? Resolve(WorkflowBinding binding, WorkflowValueResolutionContext context)
    {
        WorkflowValueResult result = new WorkflowBindingResolver().Resolve(binding, context, "/p");
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    private static WorkflowValueResolutionContext Context()
    {
        return new WorkflowValueResolutionContext(
            new Dictionary<string, JsonNode?> { ["account"] = new JsonObject { ["id"] = "ada" } },
            new Dictionary<string, JsonNode?> { ["message"] = "Hello" },
            new Dictionary<string, NodePortValueMap>
            {
                ["check"] = new(new Dictionary<string, NodePortValueSet>
                {
                    ["status"] = new([JsonValue.Create("ok")]),
                    ["null"] = new([null]),
                    ["many"] = new([JsonValue.Create(1), JsonValue.Create(2)]),
                    ["empty"] = new(),
                }),
            },
            new Dictionary<string, WorkflowIterationContext>
            {
                ["loop"] = new("loop", 0, 1, new JsonObject { ["name"] = "Ada" }, hasItem: true, count: 3),
                ["null-loop"] = new("null-loop", 1, 2, hasItem: true),
                ["no-item"] = new("no-item", 2, 3),
            });
    }
}
