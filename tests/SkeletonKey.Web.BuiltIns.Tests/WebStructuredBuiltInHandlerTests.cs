using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Runtime;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.BuiltIns.Tests;

/// <summary>Covers Phase 3 structured web handler parameter mapping.</summary>
public sealed class WebStructuredBuiltInHandlerTests
{
    /// <summary>Verifies collection fields are mapped to relative locator plans in declaration order.</summary>
    [Fact]
    public async Task ExtractCollectionMapsRelativeFieldsAndBounds()
    {
        FakeAdapter adapter = new();
        WebExtractCollectionHandler handler = new();
        NodeLocatorBinding[] bindings =
        [
            Binding("target", ".row", LocatorCardinality.OneOrMore),
            Binding("field1", ".name", LocatorCardinality.One),
            Binding("field2", "a", LocatorCardinality.One),
        ];
        JsonObject parameters = new()
        {
            ["fields"] = new JsonArray(
                new JsonObject { ["name"] = "name", ["mode"] = "text" },
                new JsonObject { ["name"] = "href", ["mode"] = "attribute", ["attribute"] = "href" }),
            ["maximumItems"] = 12,
            ["maximumOutputCharacters"] = 4096,
        };

        NodeHandlerResult result = await handler.ExecuteAsync(Request("web.extractCollection", parameters), new Context(adapter, bindings));

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.NotNull(adapter.ExtractionFields);
        Assert.Equal(["name", "href"], adapter.ExtractionFields.Select(static field => field.Name));
        Assert.Equal(WebCollectionFieldReadMode.Attribute, adapter.ExtractionFields[1].ReadMode);
        Assert.Equal("href", adapter.ExtractionFields[1].AttributeName);
        Assert.Equal(12, adapter.ExtractionRequest!.MaximumItems);
        Assert.Equal(4096, adapter.ExtractionRequest.MaximumOutputCharacters);
        Assert.Equal("Ada", result.Outputs.DataOutputs["items"].Values[0]!["name"]!.GetValue<string>());
        Assert.Equal(1, result.Outputs.DataOutputs["totalMatchedCount"].Values[0]!.GetValue<int>());
    }

    /// <summary>Verifies sequential typing preserves explicit per-key delay.</summary>
    [Fact]
    public async Task TypeForwardsSequentialDelay()
    {
        FakeAdapter adapter = new();
        WebTypeHandler handler = new();

        NodeHandlerResult result = await handler.ExecuteAsync(
            Request("web.type", new JsonObject { ["value"] = "hello", ["delayMilliseconds"] = 25 }),
            new Context(adapter, [Binding("target", "#box", LocatorCardinality.One)]));

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Equal("hello", adapter.TypeRequest!.Value);
        Assert.Equal(25, adapter.TypeRequest.DelayMilliseconds);
    }

    /// <summary>Verifies observable count waits map condition and expected count without arbitrary handler sleeps.</summary>
    [Fact]
    public async Task WaitForConditionMapsCountPredicate()
    {
        FakeAdapter adapter = new();
        WebWaitForConditionHandler handler = new();

        NodeHandlerResult result = await handler.ExecuteAsync(
            Request("web.waitForCondition", new JsonObject { ["condition"] = "countGreaterThan", ["count"] = 3, ["timeoutMilliseconds"] = 2000 }),
            new Context(adapter, [Binding("target", ".row", LocatorCardinality.Many)]));

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Equal(WebWaitConditionKind.CountGreaterThan, adapter.WaitRequest!.Condition);
        Assert.Equal(3, adapter.WaitRequest.ExpectedCount);
        Assert.Equal(4, result.Outputs.DataOutputs["result"].Values[0]!["count"]!.GetValue<int>());
    }

    /// <summary>Verifies structured collection locator slots are declared for sixteen bounded field definitions.</summary>
    [Fact]
    public void ExtractCollectionCatalogDeclaresBoundedFieldSlots()
    {
        WorkflowNodeDefinition definition = WebBuiltInWorkflowNodeCatalog.Document.Definitions.Single(static item => item.Type == "web.extractCollection");

        Assert.True(definition.Locators.ContainsKey("target"));
        for (int index = 1; index <= 16; index++)
        {
            string name = "field" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(definition.Locators.TryGetValue(name, out NodeLocatorSlotDefinition? slot));
            Assert.False(slot!.Required);
            Assert.Equal("/fields/" + (index - 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "/locator", slot.ParameterPointer);
        }
    }

    private static NodeExecutionRequest Request(string type, JsonObject parameters)
    {
        return new(new NodeExecutionIdentity("execution", "invocation", null, "workflow", "node", new(type, 1), "plan", "step", 1), parameters);
    }

    private sealed class Context(FakeAdapter adapter, IReadOnlyList<NodeLocatorBinding> locators) : INodeExecutionContext
    {
        public NodeExecutionIdentity Identity { get; } = new("execution", "invocation", null, "workflow", "node", new("web.extractCollection", 1), "plan", "step", 1);

        public INodeExecutionEventWriter Events { get; } = new EventWriter();

        public INodeResourceAccessor Resources { get; } = new ResourceAccessor(adapter);

        public INodeLocatorAccessor Locators { get; } = new RuntimeNodeLocatorAccessor(locators);
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
        public IReadOnlyList<WebCollectionFieldDefinition>? ExtractionFields { get; private set; }

        public WebCollectionExtractionRequest? ExtractionRequest { get; private set; }

        public WebTypeRequest? TypeRequest { get; private set; }

        public WebWaitForConditionRequest? WaitRequest { get; private set; }

        public ValueTask<string> NavigateAsync(WebNavigationRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(request.Url);

        public ValueTask ClickAsync(ResolvedLocatorPlan locator, WebClickRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask FillAsync(ResolvedLocatorPlan locator, WebFillRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PressAsync(ResolvedLocatorPlan locator, WebPressRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SelectOptionAsync(ResolvedLocatorPlan locator, WebSelectOptionRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCheckedAsync(ResolvedLocatorPlan locator, WebSetCheckedRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask WaitAsync(ResolvedLocatorPlan locator, WebWaitRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<string?>> GetTextAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<string?>>([]);

        public ValueTask<IReadOnlyList<string?>> GetAttributeAsync(ResolvedLocatorPlan locator, string name, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<string?>>([]);

        public ValueTask<int> GetCountAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(0);

        public ValueTask<WebScreenshotResult> ScreenshotAsync(ResolvedLocatorPlan? locator, WebScreenshotRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(new WebScreenshotResult("image/png", [1]));

        public ValueTask<WebCollectionExtractionResult> ExtractCollectionAsync(ResolvedLocatorPlan items, IReadOnlyList<WebCollectionFieldDefinition> fields, WebCollectionExtractionRequest request, CancellationToken cancellationToken = default)
        {
            ExtractionFields = fields;
            ExtractionRequest = request;
            WebCollectionItem item = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["name"] = "Ada",
                ["href"] = "/ada",
            });
            return ValueTask.FromResult(new WebCollectionExtractionResult([item], 1, false));
        }

        public ValueTask TypeAsync(ResolvedLocatorPlan locator, WebTypeRequest request, CancellationToken cancellationToken = default)
        {
            TypeRequest = request;
            return ValueTask.CompletedTask;
        }

        public ValueTask<WebWaitConditionResult> WaitForConditionAsync(ResolvedLocatorPlan locator, WebWaitForConditionRequest request, CancellationToken cancellationToken = default)
        {
            WaitRequest = request;
            return ValueTask.FromResult(new WebWaitConditionResult(4, "4"));
        }
    }

    private static NodeLocatorBinding Binding(string slotName, string selector, LocatorCardinality cardinality)
    {
        ResolvedLocatorPlan plan = new("catalog", "0.1.0", slotName, null, cardinality, [new ResolvedLocatorStrategy("css", selector: selector)]);
        return new NodeLocatorBinding(slotName, new LocatorReference("catalog", slotName, "0.1.0"), plan, true);
    }
}
