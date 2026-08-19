using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Catalog.Validation;
using SkeletonKey.Desktop.Abstractions;
using SkeletonKey.Desktop.BuiltIns;
using SkeletonKey.Desktop.FlaUI;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Runtime;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Covers the Phase 23 desktop host composition without requiring an interactive desktop.</summary>
public sealed class DesktopPhase23Tests
{
    /// <summary>Verifies desktop definitions and handlers remain a complete one-to-one set.</summary>
    [Fact]
    public void DesktopCatalogIsValidAndHasMatchingHandlers()
    {
        NodeCatalogValidationResult validation = new NodeCatalogSemanticValidator().Validate(DesktopBuiltInWorkflowNodeCatalog.Document);
        string[] definitions = DesktopBuiltInWorkflowNodeCatalog.Catalog.Definitions
            .Select(static definition => definition.Key.ToString())
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
        string[] handlers = DesktopBuiltInRuntimeHandlers.Create()
            .Select(static handler => handler.Definition.ToString())
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(validation.IsValid);
        Assert.Equal(5, definitions.Length);
        Assert.Equal(definitions, handlers);
    }

    /// <summary>Verifies launch and attach constraints apply safe deterministic defaults.</summary>
    [Fact]
    public void DesktopApplicationConstraintsAreClosedAndModeAware()
    {
        var launch = FlaUiApplicationConstraints.Parse(new JsonObject
        {
            ["mode"] = "launch",
            ["executable"] = "notepad.exe",
        });
        var attach = FlaUiApplicationConstraints.Parse(new JsonObject
        {
            ["mode"] = "attach",
            ["processId"] = 42,
        });

        Assert.True(launch.CloseOnDispose);
        Assert.False(attach.CloseOnDispose);
        Assert.Equal(30000, launch.DefaultTimeoutMilliseconds);
        Assert.Throws<ArgumentException>(() => FlaUiApplicationConstraints.Parse(new JsonObject
        {
            ["mode"] = "launch",
            ["executable"] = "notepad.exe",
            ["unknown"] = true,
        }));
    }

    /// <summary>Verifies fill delegates the value without copying sensitive text into result metadata.</summary>
    [Fact]
    public async Task DesktopFillDelegatesWithoutResultMetadata()
    {
        FakeDesktopAdapter adapter = new();
        NodeHandlerResult result = await new DesktopFillHandler().ExecuteAsync(
            Request("desktop.fill", new JsonObject { ["value"] = "sensitive desktop value" }),
            new Context(adapter));

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Equal("sensitive desktop value", adapter.FilledValue);
        Assert.Null(result.Metadata);
    }

    /// <summary>Verifies desktop text query output preserves order and explicit nulls.</summary>
    [Fact]
    public async Task DesktopGetTextPreservesOrderAndNulls()
    {
        FakeDesktopAdapter adapter = new() { TextValues = ["first", null, "third"] };
        NodeHandlerResult result = await new DesktopGetTextHandler().ExecuteAsync(
            Request("desktop.getText", new JsonObject()),
            new Context(adapter));

        IReadOnlyList<JsonNode?> values = result.Outputs.DataOutputs["result"].Values;
        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Equal("first", values[0]!.GetValue<string>());
        Assert.Null(values[1]);
        Assert.Equal("third", values[2]!.GetValue<string>());
    }

    /// <summary>Verifies the runner resolves external locator catalogs while analyzing desktop nodes.</summary>
    [Fact]
    public async Task RunnerAnalyzesDesktopWorkflowWithLocatorDirectory()
    {
        string root = RepositoryRoot();
        string workflow = Path.Combine(root, "tests", "fixtures", "desktop", "phase-023-notepad.workflow.json");
        string locators = Path.Combine(root, "tests", "fixtures", "desktop");
        StringWriter output = new();

        int exitCode = await new SkeletonKeyRunner(TextReader.Null, output, TextWriter.Null).ExecuteAsync([
            "analyze", "--file", workflow, "--locator-directory", locators,
        ]);

        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.Equal(RunnerExitCodes.Success, exitCode);
        Assert.True(envelope["accepted"]!.GetValue<bool>());
        Assert.Equal("ready", envelope["status"]!.GetValue<string>());
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo current = new(AppContext.BaseDirectory);
        while (current.Parent is not null && !File.Exists(Path.Combine(current.FullName, "SkeletonKey.sln")))
        {
            current = current.Parent;
        }

        return current.FullName;
    }

    private static NodeExecutionRequest Request(string type, JsonObject parameters)
    {
        return new(new NodeExecutionIdentity("execution", "invocation", null, "workflow", "node", new(type, 1), "plan", "step", 1), parameters);
    }

    private sealed class Context(FakeDesktopAdapter adapter) : INodeExecutionContext
    {
        public NodeExecutionIdentity Identity { get; } = new("execution", "invocation", null, "workflow", "node", new("desktop.getText", 1), "plan", "step", 1);

        public INodeExecutionEventWriter Events { get; } = new EventWriter();

        public INodeResourceAccessor Resources { get; } = new ResourceAccessor(adapter);

        public INodeLocatorAccessor Locators { get; } = new RuntimeNodeLocatorAccessor([new NodeLocatorBinding("target", new LocatorReference("catalog", "editor", "0.1.0"), Locator(), true)]);
    }

    private sealed class ResourceAccessor(FakeDesktopAdapter adapter) : INodeResourceAccessor
    {
        public IReadOnlyList<NodeResourceBinding> Bindings { get; } = Array.AsReadOnly([new NodeResourceBinding("application", "application", StandardWorkflowResourceKinds.DesktopApplication, WorkflowResourceAccessMode.Exclusive)]);

        public bool TryGetBinding(string slotName, out NodeResourceBinding? binding)
        {
            binding = Bindings[0];
            return string.Equals(slotName, "application", StringComparison.Ordinal);
        }

        public ValueTask<INodeResourceLease> AcquireAsync(string slotName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<INodeResourceLease>(new Lease(adapter));
        }
    }

    private sealed class Lease(FakeDesktopAdapter adapter) : INodeResourceLease
    {
        public INodeResourceHandle Resource { get; } = new Handle(adapter);

        public WorkflowResourceAccessMode Access => WorkflowResourceAccessMode.Exclusive;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Handle(FakeDesktopAdapter adapter) : INodeResourceHandle
    {
        public string ResourceName => "application";

        public string Kind => StandardWorkflowResourceKinds.DesktopApplication;

        public string InstanceId => "fake-desktop";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public bool TryGetAdapter<TAdapter>(out TAdapter? typedAdapter)
            where TAdapter : class
        {
            typedAdapter = adapter as TAdapter;
            return typedAdapter is not null;
        }

        public TAdapter GetRequiredAdapter<TAdapter>()
            where TAdapter : class
        {
            return (adapter as TAdapter)!;
        }
    }

    private sealed class EventWriter : INodeExecutionEventWriter
    {
        public ValueTask WriteLogAsync(WorkflowLogLevel level, string message, JsonObject? data = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask ReportProgressAsync(double? progress, string? message = null, JsonObject? data = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask EmitOutputAsync(string channel, JsonNode? payload, string? recordKey = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FakeDesktopAdapter : IDesktopApplicationAdapter
    {
        public string? FilledValue { get; private set; }

        public IReadOnlyList<string?> TextValues { get; init; } = Array.AsReadOnly(Array.Empty<string?>());

        public ValueTask ClickAsync(ResolvedLocatorPlan locator, DesktopClickRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask FillAsync(ResolvedLocatorPlan locator, DesktopFillRequest request, CancellationToken cancellationToken = default)
        {
            FilledValue = request.Value;
            return ValueTask.CompletedTask;
        }

        public ValueTask PressAsync(ResolvedLocatorPlan locator, DesktopPressRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<string?>> GetTextAsync(ResolvedLocatorPlan locator, DesktopQueryRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(TextValues);

        public ValueTask<int> GetCountAsync(ResolvedLocatorPlan locator, DesktopQueryRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(TextValues.Count);
    }

    private static ResolvedLocatorPlan Locator()
    {
        return new("catalog", "0.1.0", "editor", null, LocatorCardinality.One, [new ResolvedLocatorStrategy("role", role: "textbox")]);
    }
}
