using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Catalog;
using SkeletonKey.Catalog.Validation;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Runtime;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.BuiltIns.Tests;

/// <summary>
/// Covers essential web built-in definitions and handlers using a fake web adapter.
/// </summary>
public sealed class WebBuiltInHandlerTests
{
    /// <summary>
    /// Verifies web built-in catalog definitions are semantically valid.
    /// </summary>
    [Fact]
    public void WebCatalogDefinitionsAreValid()
    {
        Assert.True(new NodeCatalogSemanticValidator().Validate(WebBuiltInWorkflowNodeCatalog.Document).IsValid);
        Assert.Equal(27, WebBuiltInWorkflowNodeCatalog.Document.Definitions.Count);
    }

    /// <summary>
    /// Verifies frame-aware nodes expose declared locator slots for ordered frame chains.
    /// </summary>
    [Fact]
    public void FrameAwareDefinitionsExposeFrameLocatorSlots()
    {
        WorkflowNodeDefinition fill = WebBuiltInWorkflowNodeCatalog.Document.Definitions.Single(static definition => definition.Type == "web.fill" && definition.Version == 1);
        WorkflowNodeDefinition openPage = WebBuiltInWorkflowNodeCatalog.Document.Definitions.Single(static definition => definition.Type == "web.openPage" && definition.Version == 1);

        Assert.Contains("target", fill.Locators.Keys);
        for (int index = 1; index <= 5; index++)
        {
            string name = "frame" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(fill.Locators.TryGetValue(name, out NodeLocatorSlotDefinition? slot));
            Assert.False(slot.Required);
            Assert.Equal("/" + name, slot.ParameterPointer);
            Assert.Equal(LocatorUsageMode.Single, slot.Usage);
            Assert.Equal([LocatorCardinality.One], slot.AcceptedCardinalities);
        }

        Assert.DoesNotContain("frame1", openPage.Locators.Keys);
    }

    /// <summary>
    /// Verifies handlers expose exact version-one definition keys.
    /// </summary>
    [Fact]
    public void HandlersExposeExactDefinitionKeys()
    {
        Assert.Equal(
            WebBuiltInWorkflowNodeCatalog.Document.Definitions.Select(static definition => definition.Key).OrderBy(static key => key.Type, StringComparer.Ordinal),
            WebBuiltInRuntimeHandlers.Create().Select(static handler => handler.Definition).OrderBy(static key => key.Type, StringComparer.Ordinal));
    }

    /// <summary>
    /// Verifies web action nodes use the standard main/continue control-port convention.
    /// </summary>
    [Fact]
    public void WebDefinitionsFollowControlPortConvention()
    {
        foreach (WorkflowNodeDefinition definition in WebBuiltInWorkflowNodeCatalog.Document.Definitions)
        {
            Assert.Equal(["main"], definition.Inputs.Keys);
            Assert.Contains("continue", definition.Outputs.Keys);
            Assert.Equal(definition.Inputs.Count, definition.Inputs.Keys.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(definition.Outputs.Count, definition.Outputs.Keys.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    /// Verifies query handlers preserve ordered multi-value outputs and explicit null.
    /// </summary>
    [Fact]
    public async Task GetAttributePreservesOrderingAndNulls()
    {
        FakeAdapter adapter = new() { AttributeValues = ["one", null, "three"] };
        WebGetAttributeHandler handler = new();
        NodeExecutionRequest request = Request("web.getAttribute", new JsonObject { ["name"] = "data-value" });

        NodeHandlerResult result = await handler.ExecuteAsync(request, new Context(adapter), CancellationToken.None);

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Equal(["continue"], result.Outputs.ActivatedControlOutputs);
        IReadOnlyList<JsonNode?> values = result.Outputs.DataOutputs["result"].Values;
        Assert.Equal("one", values[0]!.GetValue<string>());
        Assert.Null(values[1]);
        Assert.Equal("three", values[2]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies fill delegates without exposing the sensitive value in result metadata.
    /// </summary>
    [Fact]
    public async Task FillDoesNotReturnSensitiveValue()
    {
        FakeAdapter adapter = new();
        WebFillHandler handler = new();
        NodeHandlerResult result = await handler.ExecuteAsync(Request("web.fill", new JsonObject { ["value"] = "secret" }), new Context(adapter), CancellationToken.None);

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Equal("secret", adapter.FilledValue);
        Assert.Null(result.Metadata);
    }

    /// <summary>
    /// Verifies handlers resolve declared frame slots into provider-neutral target context.
    /// </summary>
    [Fact]
    public async Task FillForwardsDeclaredFrameChain()
    {
        FakeAdapter adapter = new();
        WebFillHandler handler = new();
        NodeLocatorBinding[] bindings =
        [
            Binding("target", "#target"),
            Binding("frame1", "iframe.outer"),
            Binding("frame2", "iframe.inner"),
        ];
        NodeExecutionRequest request = Request("web.fill", new JsonObject
        {
            ["value"] = "secret",
            ["frames"] = new JsonArray("frame1", "frame2"),
        });

        NodeHandlerResult result = await handler.ExecuteAsync(request, new Context(adapter, bindings), CancellationToken.None);

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.NotNull(adapter.FillTargetContext);
        Assert.Equal(["frame1", "frame2"], adapter.FillTargetContext.Frames.Select(static frame => frame.LocatorId));
    }

    /// <summary>
    /// Verifies a frame-chain slot must be declared through locator bindings.
    /// </summary>
    [Fact]
    public async Task FillRejectsUnknownFrameSlot()
    {
        FakeAdapter adapter = new();
        WebFillHandler handler = new();
        NodeExecutionRequest request = Request("web.fill", new JsonObject
        {
            ["value"] = "secret",
            ["frames"] = new JsonArray("frame1"),
        });

        NodeHandlerResult result = await handler.ExecuteAsync(request, new Context(adapter), CancellationToken.None);

        Assert.Equal(NodeHandlerCompletionStatus.Failed, result.Status);
        Assert.Equal(WebAutomationErrorCodes.FrameNotFound, result.Error!.Code);
    }

    private static NodeExecutionRequest Request(string type, JsonObject parameters)
    {
        return new(new NodeExecutionIdentity("execution", "invocation", null, "workflow", "node", new(type, 1), "plan", "step", 1), parameters);
    }

    private sealed class Context(FakeAdapter adapter, IReadOnlyList<NodeLocatorBinding>? locators = null) : INodeExecutionContext
    {
        public NodeExecutionIdentity Identity { get; } = new("execution", "invocation", null, "workflow", "node", new("web.getText", 1), "plan", "step", 1);

        public INodeExecutionEventWriter Events { get; } = new EventWriter();

        public INodeResourceAccessor Resources { get; } = new ResourceAccessor(adapter);

        public INodeLocatorAccessor Locators { get; } = new RuntimeNodeLocatorAccessor(locators ?? [Binding("target", "#target")]);
    }

    private sealed class ResourceAccessor(FakeAdapter adapter) : INodeResourceAccessor
    {
        public IReadOnlyList<NodeResourceBinding> Bindings { get; } = Array.AsReadOnly([new NodeResourceBinding("page", "page", StandardWorkflowResourceKinds.WebPage, WorkflowResourceAccessMode.Exclusive)]);

        public bool TryGetBinding(string slotName, out NodeResourceBinding? binding)
        {
            binding = Bindings[0];
            return string.Equals(slotName, "page", StringComparison.Ordinal);
        }

        public ValueTask<INodeResourceLease> AcquireAsync(string slotName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<INodeResourceLease>(new Lease(adapter));
        }
    }

    private sealed class Lease(FakeAdapter adapter) : INodeResourceLease
    {
        public INodeResourceHandle Resource { get; } = new Handle(adapter);

        public WorkflowResourceAccessMode Access => WorkflowResourceAccessMode.Exclusive;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Handle(FakeAdapter adapter) : INodeResourceHandle
    {
        public string ResourceName => "page";

        public string Kind => StandardWorkflowResourceKinds.WebPage;

        public string InstanceId => "fake";

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

    private sealed class FakeAdapter : IWebPageAdapter
    {
        public IReadOnlyList<string?> AttributeValues { get; init; } = Array.AsReadOnly(Array.Empty<string?>());

        public string? FilledValue { get; private set; }

        public WebTargetContext? FillTargetContext { get; private set; }

        public ValueTask<string> NavigateAsync(WebNavigationRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(request.Url);

        public ValueTask ClickAsync(ResolvedLocatorPlan locator, WebClickRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask FillAsync(ResolvedLocatorPlan locator, WebFillRequest request, CancellationToken cancellationToken = default)
        {
            FilledValue = request.Value;
            FillTargetContext = request.TargetContext;
            return ValueTask.CompletedTask;
        }

        public ValueTask PressAsync(ResolvedLocatorPlan locator, WebPressRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SelectOptionAsync(ResolvedLocatorPlan locator, WebSelectOptionRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCheckedAsync(ResolvedLocatorPlan locator, WebSetCheckedRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask WaitAsync(ResolvedLocatorPlan locator, WebWaitRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<string?>> GetTextAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<string?>>(["text"]);

        public ValueTask<IReadOnlyList<string?>> GetAttributeAsync(ResolvedLocatorPlan locator, string name, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(AttributeValues);

        public ValueTask<int> GetCountAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(3);

        public ValueTask<WebScreenshotResult> ScreenshotAsync(ResolvedLocatorPlan? locator, WebScreenshotRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(new WebScreenshotResult("image/png", [1, 2, 3]));
    }

    private static ResolvedLocatorPlan Locator()
    {
        return new("catalog", "0.1.0", "target", null, LocatorCardinality.One, [new ResolvedLocatorStrategy("css", selector: "#target")]);
    }

    private static ResolvedLocatorPlan Locator(string id, string selector)
    {
        return new("catalog", "0.1.0", id, null, LocatorCardinality.One, [new ResolvedLocatorStrategy("css", selector: selector)]);
    }

    private static NodeLocatorBinding Binding(string slotName, string selector)
    {
        return new NodeLocatorBinding(slotName, new LocatorReference("catalog", slotName, "0.1.0"), Locator(slotName, selector), true);
    }
}
