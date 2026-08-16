using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Bindings;

namespace SkeletonKey.Binding.Tests;

/// <summary>
/// Covers structured workflow binding parsing and inspection.
/// </summary>
public sealed class WorkflowBindingReaderTests
{
    private readonly WorkflowBindingReader _reader = new();

    /// <summary>
    /// Verifies valid input bindings are recognized.
    /// </summary>
    [Fact]
    public void RecognizesValidInputBinding()
    {
        WorkflowBinding binding = _reader.Read(Binding("input", "account"));

        Assert.Equal(WorkflowBindingSource.Input, binding.Source);
        Assert.Equal("account", binding.Name);
    }

    /// <summary>
    /// Verifies valid variable bindings are recognized.
    /// </summary>
    [Fact]
    public void RecognizesValidVariableBinding()
    {
        WorkflowBinding binding = _reader.Read(Binding("variable", "message"));

        Assert.Equal(WorkflowBindingSource.Variable, binding.Source);
        Assert.Equal("message", binding.Name);
    }

    /// <summary>
    /// Verifies valid node bindings are recognized.
    /// </summary>
    [Fact]
    public void RecognizesValidNodeBinding()
    {
        JsonObject wrapper = new()
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = "node",
                ["node"] = "check",
                ["port"] = "result",
            },
        };

        WorkflowBinding binding = _reader.Read(wrapper);

        Assert.Equal(WorkflowBindingSource.Node, binding.Source);
        Assert.Equal("check", binding.Node);
        Assert.Equal("result", binding.Port);
    }

    /// <summary>
    /// Verifies valid iteration bindings are recognized.
    /// </summary>
    [Fact]
    public void RecognizesValidIterationBinding()
    {
        JsonObject wrapper = IterationBinding("process-contacts");

        WorkflowBinding binding = _reader.Read(wrapper);

        Assert.Equal(WorkflowBindingSource.Iteration, binding.Source);
        Assert.Equal("process-contacts", binding.Iteration);
        Assert.Equal("/item/name", binding.Path);
    }

    /// <summary>
    /// Verifies malformed binding wrappers are rejected.
    /// </summary>
    [Fact]
    public void RejectsMalformedBindingWrapper()
    {
        JsonObject wrapper = new()
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = "input",
                ["name"] = "account",
            },
            ["other"] = true,
        };

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies unknown binding properties are rejected.
    /// </summary>
    [Fact]
    public void RejectsUnknownBindingProperty()
    {
        JsonObject wrapper = Binding("input", "account");
        wrapper["$binding"]!["extra"] = true;

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies bindings without sources are rejected.
    /// </summary>
    [Fact]
    public void RejectsMissingBindingSource()
    {
        JsonObject wrapper = new()
        {
            ["$binding"] = new JsonObject
            {
                ["name"] = "account",
            },
        };

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies incompatible input binding properties are rejected.
    /// </summary>
    [Fact]
    public void RejectsIncompatibleInputBindingProperties()
    {
        JsonObject wrapper = Binding("input", "account");
        wrapper["$binding"]!["node"] = "node";

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies incompatible node binding properties are rejected.
    /// </summary>
    [Fact]
    public void RejectsIncompatibleNodeBindingProperties()
    {
        JsonObject wrapper = new()
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = "node",
                ["node"] = "node",
                ["port"] = "result",
                ["name"] = "account",
            },
        };

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies iteration bindings require an iteration identifier.
    /// </summary>
    [Fact]
    public void RejectsIterationBindingWithoutIterationId()
    {
        JsonObject wrapper = new()
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = "iteration",
            },
        };

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies iteration bindings reject input-style names.
    /// </summary>
    [Fact]
    public void RejectsIterationBindingWithName()
    {
        JsonObject wrapper = IterationBinding("process-contacts");
        wrapper["$binding"]!["name"] = "item";

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies iteration bindings reject node identifiers.
    /// </summary>
    [Fact]
    public void RejectsIterationBindingWithNode()
    {
        JsonObject wrapper = IterationBinding("process-contacts");
        wrapper["$binding"]!["node"] = "node";

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies iteration bindings reject node ports.
    /// </summary>
    [Fact]
    public void RejectsIterationBindingWithPort()
    {
        JsonObject wrapper = IterationBinding("process-contacts");
        wrapper["$binding"]!["port"] = "result";

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.Read(wrapper));
    }

    /// <summary>
    /// Verifies explicit null defaults are preserved.
    /// </summary>
    [Fact]
    public void PreservesExplicitNullDefault()
    {
        JsonObject wrapper = Binding("input", "displayName");
        wrapper["$binding"]!["onMissing"] = "default";
        wrapper["$binding"]!["default"] = null;

        WorkflowBinding binding = _reader.Read(wrapper);

        Assert.True(binding.HasDefault);
        Assert.Null(binding.Default);
    }

    /// <summary>
    /// Verifies default values are defensively cloned.
    /// </summary>
    [Fact]
    public void DefensivelyClonesDefaultValues()
    {
        JsonObject defaultValue = new()
        {
            ["value"] = 1,
        };
        JsonObject wrapper = Binding("input", "displayName");
        wrapper["$binding"]!["onMissing"] = "default";
        wrapper["$binding"]!["default"] = defaultValue;

        WorkflowBinding binding = _reader.Read(wrapper);
        defaultValue["value"] = 2;
        binding.Default!["value"] = 3;

        Assert.Equal(1, binding.Default!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies nested bindings are discovered in deterministic document order.
    /// </summary>
    [Fact]
    public void FindsNestedBindingsInDocumentOrder()
    {
        JsonObject value = new()
        {
            ["first"] = Binding("input", "one"),
            ["items"] = new JsonArray(Binding("variable", "two")),
            ["last"] = Binding("input", "three"),
        };

        IReadOnlyList<WorkflowBindingOccurrence> occurrences = _reader.FindBindings(value);

        Assert.Equal(["/first", "/items/0", "/last"], occurrences.Select(static occurrence => occurrence.Path));
    }

    /// <summary>
    /// Verifies nested iteration bindings are discovered in deterministic document order.
    /// </summary>
    [Fact]
    public void FindsNestedIterationBindings()
    {
        JsonObject value = new()
        {
            ["item"] = IterationBinding("process-contacts"),
        };

        WorkflowBindingOccurrence occurrence = Assert.Single(_reader.FindBindings(value));

        Assert.Equal("/item", occurrence.Path);
        Assert.Equal(WorkflowBindingSource.Iteration, occurrence.Binding.Source);
    }

    /// <summary>
    /// Verifies binding paths are reported as JSON Pointers.
    /// </summary>
    [Fact]
    public void ReportsBindingPathsAsJsonPointers()
    {
        JsonObject value = new()
        {
            ["item"] = Binding("input", "one"),
        };

        Assert.Equal("/item", _reader.FindBindings(value)[0].Path);
    }

    /// <summary>
    /// Verifies object keys are escaped in occurrence paths.
    /// </summary>
    [Fact]
    public void EscapesObjectKeysInOccurrencePaths()
    {
        JsonObject value = new()
        {
            ["a/b~c"] = Binding("input", "one"),
        };

        Assert.Equal("/a~1b~0c", _reader.FindBindings(value)[0].Path);
    }

    /// <summary>
    /// Verifies bindings inside literal wrappers are not inspected.
    /// </summary>
    [Fact]
    public void DoesNotInspectInsideLiteralWrapper()
    {
        JsonObject value = new()
        {
            ["literal"] = new JsonObject
            {
                ["$literal"] = Binding("input", "hidden"),
            },
        };

        Assert.Empty(_reader.FindBindings(value));
    }

    /// <summary>
    /// Verifies iteration bindings inside literal wrappers are not inspected.
    /// </summary>
    [Fact]
    public void DoesNotInspectIterationBindingInsideLiteralWrapper()
    {
        JsonObject value = new()
        {
            ["literal"] = new JsonObject
            {
                ["$literal"] = IterationBinding("process-contacts"),
            },
        };

        Assert.Empty(_reader.FindBindings(value));
    }

    /// <summary>
    /// Verifies valid literal wrappers are recognized.
    /// </summary>
    [Fact]
    public void RecognizesValidLiteralWrapper()
    {
        JsonObject wrapper = new()
        {
            ["$literal"] = new JsonObject
            {
                ["$binding"] = new JsonObject(),
            },
        };

        Assert.True(_reader.IsLiteral(wrapper));
        Assert.Empty(_reader.FindBindings(wrapper));
    }

    /// <summary>
    /// Verifies malformed literal wrappers are rejected.
    /// </summary>
    [Fact]
    public void RejectsMalformedLiteralWrapper()
    {
        JsonObject wrapper = new()
        {
            ["$literal"] = new JsonObject(),
            ["sibling"] = true,
        };

        Assert.Throws<WorkflowBindingFormatException>(() => _reader.FindBindings(wrapper));
    }

    /// <summary>
    /// Verifies reading and scanning do not mutate source JSON.
    /// </summary>
    [Fact]
    public void DoesNotMutateSourceJson()
    {
        JsonObject value = new()
        {
            ["binding"] = Binding("input", "one"),
        };
        string before = value.ToJsonString();

        _ = _reader.FindBindings(value);

        Assert.Equal(before, value.ToJsonString());
    }

    /// <summary>
    /// Verifies one reader instance can scan concurrently.
    /// </summary>
    [Fact]
    public async Task SingleReaderInstanceIsThreadSafe()
    {
        JsonObject value = new()
        {
            ["binding"] = Binding("input", "one"),
        };

        WorkflowBindingOccurrence[][] results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => _reader.FindBindings(value).ToArray())));

        Assert.All(results, result => Assert.Equal("/binding", result[0].Path));
    }

    /// <summary>
    /// Verifies repeated scans produce deterministic results.
    /// </summary>
    [Fact]
    public void RepeatedScansProduceDeterministicResults()
    {
        JsonObject value = new()
        {
            ["first"] = Binding("input", "one"),
            ["second"] = Binding("variable", "two"),
        };

        Assert.Equal(
            _reader.FindBindings(value).Select(static occurrence => occurrence.Path),
            _reader.FindBindings(value).Select(static occurrence => occurrence.Path));
    }

    private static JsonObject Binding(string source, string name)
    {
        return new JsonObject
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = source,
                ["name"] = name,
            },
        };
    }

    private static JsonObject IterationBinding(string iteration)
    {
        return new JsonObject
        {
            ["$binding"] = new JsonObject
            {
                ["source"] = "iteration",
                ["iteration"] = iteration,
                ["path"] = "/item/name",
            },
        };
    }
}
