using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Runtime;
using SkeletonKey.Materialization;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Default;
using SkeletonKey.Validation;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Web.BuiltIns;
using SkeletonKey.Web.Playwright;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.Integration.Tests;

/// <summary>Exercises Phase 3 structured web primitives against real Chromium.</summary>
public sealed class StructuredChromiumWorkflowTests
{
    /// <summary>Verifies nested field locator wrappers survive analysis/materialization and remain relative to each parent item.</summary>
    [Fact]
    public async Task ExtractCollectionPreservesParentFieldAlignmentWhenEnabled()
    {
        if (!Enabled())
        {
            return;
        }

        LocatorDocument locators = new(
            id: "structured",
            locators: new Dictionary<string, LocatorDefinition>(StringComparer.Ordinal)
            {
                ["rows"] = new(cardinality: LocatorCardinality.OneOrMore, strategies: [new("css", selector: ".row")]),
                ["name"] = new(strategies: [new("css", selector: ".name")]),
                ["link"] = new(strategies: [new("css", selector: "a")]),
            });
        LocatorPlanResolver resolver = new(new ImmutableLocatorDocumentRepository([locators]));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. WebBuiltInWorkflowNodeCatalog.Catalog.Definitions]);
        IReadOnlyList<INodeHandler> handlers = [.. BuiltInRuntimeHandlers.Create(), .. WebBuiltInRuntimeHandlers.Create()];
        WorkflowDocument workflow = CollectionWorkflow();

        DefaultWorkflowRuntime runtime = new(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(locatorResolver: resolver),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver(handlers),
            new NodeParameterMaterializer(),
            resourceProviders: [new PlaywrightPageResourceProvider()],
            locatorResolver: resolver);

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(workflow, "phase3-collection", "phase3-collection-plan"));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        NodeExecutionResult extract = result.NodeResults.Single(static node => node.NodeId == "extract" && node.Status == NodeExecutionStatus.Succeeded);
        IReadOnlyList<JsonNode?> items = extract.Outputs["items"] is JsonArray array ? array.ToArray() : throw new InvalidOperationException("Expected collection output.");
        Assert.Equal(2, items.Count);
        Assert.Equal("Ada", items[0]!["name"]!.GetValue<string>());
        Assert.Equal("/ada", items[0]!["href"]!.GetValue<string>());
        Assert.Equal("Grace", items[1]!["name"]!.GetValue<string>());
        Assert.Equal("/grace", items[1]!["href"]!.GetValue<string>());
    }

    /// <summary>Verifies virtualized-list continuation, explicit scroll metrics, event-sensitive input modes, and observable waits without test sleeps.</summary>
    [Fact]
    public async Task StructuredAdapterHandlesVirtualizedListTypingAndWaitsWhenEnabled()
    {
        if (!Enabled())
        {
            return;
        }

        WorkflowResourceDefinition definition = new(
            StandardWorkflowResourceKinds.WebPage,
            capabilities:
            [
                StandardWorkflowResourceCapabilities.WebNavigation,
                StandardWorkflowResourceCapabilities.WebActions,
                StandardWorkflowResourceCapabilities.WebLocators,
                StandardWorkflowResourceCapabilities.WebText,
                StandardWorkflowResourceCapabilities.WebAttributes,
                StandardWorkflowResourceCapabilities.WebForms,
            ],
            constraints: new JsonObject
            {
                ["engine"] = "chromium",
                ["visibility"] = "headless",
                ["profile"] = "ephemeral",
                ["defaultTimeoutMilliseconds"] = 10000,
            });
        PlaywrightPageResourceProvider provider = new();
        await using IWorkflowRuntimeResourceInstance resource = await provider.CreateAsync(
            new WorkflowRuntimeResourceRequest("phase3-adapter", "phase3-adapter-invocation", "phase3-adapter-workflow", "page", definition));
        IWebPageAdapter adapter = resource.CreateHandle().GetRequiredAdapter<IWebPageAdapter>();

        await adapter.NavigateAsync(new WebNavigationRequest(VirtualizedHtmlUrl(), TimeoutMilliseconds: 10000));

        ResolvedLocatorPlan rows = Plan("rows", ".row", LocatorCardinality.OneOrMore);
        ResolvedLocatorPlan name = Plan("name", ".name", LocatorCardinality.One);
        ResolvedLocatorPlan scroller = Plan("scroller", "#scroller", LocatorCardinality.One);
        WebCollectionFieldDefinition[] fields = [new("name", name)];

        WebCollectionExtractionResult current = await adapter.ExtractCollectionAsync(rows, fields, new WebCollectionExtractionRequest(new WebTargetContext(), MaximumItems: 20));
        for (int iteration = 0; iteration < 5 && current.TotalMatchedCount < 7; iteration++)
        {
            int previous = current.TotalMatchedCount;
            _ = await adapter.ScrollAsync(scroller, new WebScrollRequest(DeltaY: 10000));
            _ = await adapter.WaitForConditionAsync(rows, new WebWaitForConditionRequest(
                WebWaitConditionKind.CountGreaterThan,
                ExpectedCount: previous,
                TimeoutMilliseconds: 3000,
                PollIntervalMilliseconds: 25));
            current = await adapter.ExtractCollectionAsync(rows, fields, new WebCollectionExtractionRequest(new WebTargetContext(), MaximumItems: 20));
        }

        Assert.Equal(7, current.TotalMatchedCount);
        Assert.Equal("item-1", current.Items[0].Fields["name"]);
        Assert.Equal("item-7", current.Items[^1].Fields["name"]);

        ResolvedLocatorPlan box = Plan("box", "#box", LocatorCardinality.One);
        ResolvedLocatorPlan log = Plan("log", "#eventlog", LocatorCardinality.One);
        ResolvedLocatorPlan asyncButton = Plan("async", "#async", LocatorCardinality.One);

        await adapter.FillAsync(box, new WebFillRequest("A"));
        string afterFill = Assert.Single(await adapter.GetTextAsync(log))!;
        Assert.DoesNotContain("keydown:A", afterFill, StringComparison.Ordinal);

        await adapter.ClearAsync(box, new WebElementActionRequest());
        await adapter.TypeAsync(box, new WebTypeRequest("BC"));
        string afterType = Assert.Single(await adapter.GetTextAsync(log))!;
        Assert.Contains("keydown:B", afterType, StringComparison.Ordinal);
        Assert.Contains("keydown:C", afterType, StringComparison.Ordinal);

        await adapter.ClearAsync(box, new WebElementActionRequest());
        await adapter.FocusAsync(box, new WebElementActionRequest());
        await adapter.InsertTextAsync(box, new WebInsertTextRequest("D"));
        string afterInsert = Assert.Single(await adapter.GetTextAsync(log))!;
        Assert.DoesNotContain("keydown:D", afterInsert, StringComparison.Ordinal);

        await adapter.ClickAsync(asyncButton, new WebClickRequest());
        WebWaitConditionResult wait = await adapter.WaitForConditionAsync(box, new WebWaitForConditionRequest(
            WebWaitConditionKind.ValueEquals,
            ExpectedValue: "ready",
            TimeoutMilliseconds: 3000,
            PollIntervalMilliseconds: 25));
        Assert.Equal("ready", wait.ActualValue);

        await adapter.ScrollIntoViewAsync(rows, new WebElementActionRequest(ElementIndex: 6));
    }

    private static bool Enabled()
    {
        return string.Equals(Environment.GetEnvironmentVariable("SKELETONKEY_PLAYWRIGHT_SMOKE"), "1", StringComparison.Ordinal);
    }

    private static WorkflowDocument CollectionWorkflow()
    {
        const string html = """
            <html><body>
              <div class='row'><span class='name'>Ada</span><a href='/ada'>Profile</a></div>
              <div class='row'><span class='name'>Grace</span><a href='/grace'>Profile</a></div>
            </body></html>
            """;
        string url = "data:text/html," + Uri.EscapeDataString(html);
        JsonObject page = new() { ["$resource"] = new JsonObject { ["name"] = "page" } };
        return new WorkflowDocument(
            id: "phase3-collection-workflow",
            name: "Phase 3 Collection Workflow",
            resources: new Dictionary<string, WorkflowResourceDefinition>(StringComparer.Ordinal)
            {
                ["page"] = new(
                    StandardWorkflowResourceKinds.WebPage,
                    capabilities:
                    [
                        StandardWorkflowResourceCapabilities.WebNavigation,
                        StandardWorkflowResourceCapabilities.WebLocators,
                        StandardWorkflowResourceCapabilities.WebText,
                        StandardWorkflowResourceCapabilities.WebAttributes,
                        StandardWorkflowResourceCapabilities.WebForms,
                    ],
                    constraints: new JsonObject { ["engine"] = "chromium", ["visibility"] = "headless", ["profile"] = "ephemeral" }),
            },
            nodes:
            [
                new("start", "core.start", 1),
                new("navigate", "web.navigate", 1, parameters: new JsonObject { ["page"] = page.DeepClone(), ["url"] = url }),
                new("extract", "web.extractCollection", 1, parameters: new JsonObject
                {
                    ["page"] = page.DeepClone(),
                    ["target"] = Locator("rows"),
                    ["fields"] = new JsonArray(
                        new JsonObject { ["name"] = "name", ["locator"] = Locator("name"), ["mode"] = "text" },
                        new JsonObject { ["name"] = "href", ["locator"] = Locator("link"), ["mode"] = "attribute", ["attribute"] = "href" }),
                }),
                new("return", "core.return", 1, parameters: new JsonObject { ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" } }),
            ],
            connections:
            [
                Connect("start", "main", "navigate", "main"),
                Connect("navigate", "continue", "extract", "main"),
                Connect("extract", "continue", "return", "main"),
            ]);
    }

    private static string VirtualizedHtmlUrl()
    {
        const string html = """
            <html><body>
              <div id='scroller' style='height:80px;width:240px;overflow:auto;border:1px solid #ccc'>
                <div id='rows'></div>
              </div>
              <input id='box' />
              <button id='async'>Async</button>
              <div id='eventlog'></div>
              <script>
                const scroller = document.getElementById('scroller');
                const rows = document.getElementById('rows');
                let next = 1;
                function append(count) {
                  for (let i = 0; i < count && next <= 7; i++, next++) {
                    const row = document.createElement('div');
                    row.className = 'row';
                    row.style.height = '40px';
                    row.innerHTML = "<span class='name'>item-" + next + "</span>";
                    rows.appendChild(row);
                  }
                }
                append(3);
                scroller.addEventListener('scroll', () => {
                  if (scroller.scrollTop + scroller.clientHeight >= scroller.scrollHeight - 2 && next <= 7) {
                    append(2);
                  }
                });

                const box = document.getElementById('box');
                const eventlog = document.getElementById('eventlog');
                box.addEventListener('keydown', e => eventlog.textContent += 'keydown:' + e.key + '|');
                box.addEventListener('input', () => eventlog.textContent += 'input:' + box.value + '|');
                document.getElementById('async').addEventListener('click', () => {
                  setTimeout(() => {
                    box.value = 'ready';
                    box.dispatchEvent(new Event('input', { bubbles: true }));
                  }, 100);
                });
              </script>
            </body></html>
            """;
        return "data:text/html," + Uri.EscapeDataString(html);
    }

    private static ResolvedLocatorPlan Plan(string id, string selector, LocatorCardinality cardinality)
    {
        return new("structured-direct", "0.1.0", id, null, cardinality, [new ResolvedLocatorStrategy("css", selector: selector)]);
    }

    private static JsonObject Locator(string id)
    {
        return new JsonObject { ["$locator"] = new JsonObject { ["catalog"] = "structured", ["version"] = "0.1.0", ["id"] = id } };
    }

    private static WorkflowConnection Connect(string sourceNode, string sourcePort, string targetNode, string targetPort)
    {
        return new(new WorkflowEndpoint(sourceNode, sourcePort), new WorkflowEndpoint(targetNode, targetPort));
    }
}
