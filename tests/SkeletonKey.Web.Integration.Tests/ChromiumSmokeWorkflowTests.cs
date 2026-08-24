using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Runtime;
using SkeletonKey.Materialization;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Default;
using SkeletonKey.Validation;
using SkeletonKey.Web.BuiltIns;
using SkeletonKey.Web.Playwright;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.Integration.Tests;

/// <summary>
/// Contains the explicit opt-in Chromium smoke workflow.
/// </summary>
public sealed class ChromiumSmokeWorkflowTests
{
    /// <summary>
    /// Verifies a deterministic browser workflow against a data URL when explicitly requested.
    /// </summary>
    [Fact]
    public async Task ChromiumWorkflowSmokeSucceedsWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SKELETONKEY_PLAYWRIGHT_SMOKE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        LocatorDocument locators = new(
            id: "smoke",
            locators: new Dictionary<string, LocatorDefinition>(StringComparer.Ordinal)
            {
                ["input"] = new(strategies: [new("label", value: "Name"), new("css", selector: "#name")]),
                ["button"] = new(strategies: [new("role", role: "button", name: "Submit"), new("css", selector: "#submit")]),
                ["output"] = new(strategies: [new("test-id", value: "output"), new("css", selector: "#output")]),
                ["select"] = new(strategies: [new("css", selector: "#choice")]),
                ["checkbox"] = new(strategies: [new("label", value: "Agree"), new("css", selector: "#agree")]),
                ["items"] = new(cardinality: LocatorCardinality.OneOrMore, strategies: [new("css", selector: ".item")]),
                ["firstItem"] = new(strategies: [new("css", selector: ".item:first-child")]),
            });
        LocatorPlanResolver resolver = new(new ImmutableLocatorDocumentRepository([locators]));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. WebBuiltInWorkflowNodeCatalog.Catalog.Definitions]);
        IReadOnlyList<INodeHandler> handlers = [.. BuiltInRuntimeHandlers.Create(), .. WebBuiltInRuntimeHandlers.Create()];
        WorkflowDocument workflow = Workflow();
        WorkflowValidationResult validation = new WorkflowSemanticValidator().Validate(workflow);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors.Select(static issue => $"{issue.Code} {issue.Path}: {issue.Message}")));

        DefaultWorkflowAnalyzer analyzer = new(locatorResolver: resolver);
        WorkflowAnalysisResult analysis = analyzer.Analyze(workflow, catalog);
        Assert.True(analysis.CanPlanExecution, string.Join(Environment.NewLine, analysis.Errors.Select(static issue => $"{issue.Code} {issue.Path} {issue.NodeId}: {issue.Message}")));

        DefaultWorkflowRuntime runtime = new(
            new WorkflowSemanticValidator(),
            analyzer,
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver(handlers),
            new NodeParameterMaterializer(),
            options: new WorkflowRuntimeOptions(maximumExecutedNodeAttempts: 200),
            resourceProviders: [new PlaywrightPageResourceProvider()],
            locatorResolver: resolver);

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(workflow, "smoke-execution", "smoke-plan"));

        Assert.True(
            result.Result.Status == WorkflowExecutionStatus.Succeeded,
            result.Result.Error is null ? "Workflow failed without an error." : $"{result.Result.Error.Code}: {result.Result.Error.Message} ({result.Result.Error.NodeId})");
        Assert.Equal("Hello Ada", Output(result, "text", "result"));
        Assert.Equal("first", Output(result, "attr", "result"));
        Assert.Equal(3, OutputNode(result, "count").Outputs["count"]!.GetValue<int>());
        Assert.Equal("done", result.Result.Outcome!.Code);
        Assert.Equal(["start", "navigate", "fill", "press", "select", "check", "click", "wait", "text", "attr", "count", "shot", "return"], result.NodeResults.Where(static node => node.Status == NodeExecutionStatus.Succeeded).Select(static node => node.NodeId));
    }

    /// <summary>
    /// Verifies a browser-backed workflow can terminate with the structural core.end node used by declarative workflows.
    /// </summary>
    [Fact]
    public async Task ChromiumWorkflowCanTerminateWithCoreEndWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SKELETONKEY_PLAYWRIGHT_SMOKE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        LocatorDocument locators = new(
            id: "end-smoke",
            locators: new Dictionary<string, LocatorDefinition>(StringComparer.Ordinal)
            {
                ["heading"] = new(strategies: [new("css", selector: "#heading")]),
            });
        LocatorPlanResolver resolver = new(new ImmutableLocatorDocumentRepository([locators]));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. WebBuiltInWorkflowNodeCatalog.Catalog.Definitions]);
        IReadOnlyList<INodeHandler> handlers = [.. BuiltInRuntimeHandlers.Create(), .. WebBuiltInRuntimeHandlers.Create()];
        WorkflowDocument workflow = EndWorkflow();
        WorkflowValidationResult validation = new WorkflowSemanticValidator().Validate(workflow);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors.Select(static issue => $"{issue.Code} {issue.Path}: {issue.Message}")));

        DefaultWorkflowAnalyzer analyzer = new(locatorResolver: resolver);
        WorkflowAnalysisResult analysis = analyzer.Analyze(workflow, catalog);
        Assert.True(analysis.CanPlanExecution, string.Join(Environment.NewLine, analysis.Errors.Select(static issue => $"{issue.Code} {issue.Path} {issue.NodeId}: {issue.Message}")));

        DefaultWorkflowRuntime runtime = new(
            new WorkflowSemanticValidator(),
            analyzer,
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver(handlers),
            new NodeParameterMaterializer(),
            resourceProviders: [new PlaywrightPageResourceProvider()],
            locatorResolver: resolver);

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(workflow, "end-smoke-execution", "end-smoke-plan"));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Null(result.Result.Error);
        Assert.Null(result.Result.Outcome);
        Assert.Equal("SkeletonKey", Output(result, "text", "result"));
        Assert.Equal(["start", "navigate", "text", "end"], result.NodeResults.Where(static node => node.Status == NodeExecutionStatus.Succeeded).Select(static node => node.NodeId));
    }

    private static WorkflowDocument EndWorkflow()
    {
        const string html = "<html><body><h1 id='heading'>SkeletonKey</h1></body></html>";
        string url = "data:text/html," + Uri.EscapeDataString(html);
        return new(
            id: "web-end-smoke",
            name: "Web End Smoke",
            resources: new Dictionary<string, WorkflowResourceDefinition>(StringComparer.Ordinal)
            {
                ["page"] = new(
                    StandardWorkflowResourceKinds.WebPage,
                    capabilities:
                    [
                        StandardWorkflowResourceCapabilities.WebNavigation,
                        StandardWorkflowResourceCapabilities.WebLocators,
                        StandardWorkflowResourceCapabilities.WebText,
                    ],
                    constraints: new JsonObject { ["engine"] = "chromium", ["visibility"] = "headless", ["profile"] = "ephemeral", ["defaultTimeoutMilliseconds"] = 10000 }),
            },
            nodes:
            [
                new("start", "core.start", 1),
                Node("navigate", "web.navigate", new JsonObject { ["url"] = url }),
                Node("text", "web.getText", new JsonObject { ["target"] = new JsonObject { ["$locator"] = new JsonObject { ["catalog"] = "end-smoke", ["version"] = "0.1.0", ["id"] = "heading" } } }),
                new("end", "core.end", 1),
            ],
            connections:
            [
                Connect("start", "navigate", "main"),
                Connect("navigate", "text", "continue"),
                Connect("text", "end", "continue"),
            ],
            outputs: new Dictionary<string, SkeletonKey.Workflow.Outputs.WorkflowOutputDefinition>(StringComparer.Ordinal)
            {
                ["heading"] = new(SkeletonKey.Workflow.Outputs.WorkflowOutputMode.Single, new WorkflowEndpoint("text", "result")),
            });
    }

    private static WorkflowDocument Workflow()
    {
        return new(
            id: "web-smoke",
            name: "Web Smoke",
            resources: new Dictionary<string, WorkflowResourceDefinition>(StringComparer.Ordinal)
            {
                ["page"] = new(
                    StandardWorkflowResourceKinds.WebPage,
                    capabilities:
                    [
                        StandardWorkflowResourceCapabilities.WebNavigation,
                        StandardWorkflowResourceCapabilities.WebActions,
                        StandardWorkflowResourceCapabilities.WebLocators,
                        StandardWorkflowResourceCapabilities.WebText,
                        StandardWorkflowResourceCapabilities.WebAttributes,
                        StandardWorkflowResourceCapabilities.WebForms,
                        StandardWorkflowResourceCapabilities.WebScreenshot,
                    ],
                    constraints: new JsonObject { ["engine"] = "chromium", ["visibility"] = "headless", ["profile"] = "ephemeral", ["defaultTimeoutMilliseconds"] = 10000 }),
            },
            nodes:
            [
                new("start", "core.start", 1),
                Node("navigate", "web.navigate", new JsonObject { ["url"] = HtmlUrl() }),
                Node("fill", "web.fill", new JsonObject { ["target"] = Locator("input"), ["value"] = "Ada" }),
                Node("press", "web.press", new JsonObject { ["target"] = Locator("input"), ["key"] = "Tab" }),
                Node("select", "web.selectOption", new JsonObject { ["target"] = Locator("select"), ["value"] = "b" }),
                Node("check", "web.setChecked", new JsonObject { ["target"] = Locator("checkbox"), ["checked"] = true }),
                Node("click", "web.click", new JsonObject { ["target"] = Locator("button") }),
                Node("wait", "web.wait", new JsonObject { ["target"] = Locator("output"), ["state"] = "visible" }),
                Node("text", "web.getText", new JsonObject { ["target"] = Locator("output") }),
                Node("attr", "web.getAttribute", new JsonObject { ["target"] = Locator("firstItem"), ["name"] = "data-value" }),
                Node("count", "web.getCount", new JsonObject { ["target"] = Locator("items") }),
                Node("shot", "web.screenshot", new JsonObject { ["maximumBytes"] = 1000000 }),
                new("return", "core.return", 1, parameters: new JsonObject { ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" } }),
            ],
            connections:
            [
                Connect("start", "navigate", "main"),
                Connect("navigate", "fill", "continue"),
                Connect("fill", "press", "continue"),
                Connect("press", "select", "continue"),
                Connect("select", "check", "continue"),
                Connect("check", "click", "continue"),
                Connect("click", "wait", "continue"),
                Connect("wait", "text", "continue"),
                Connect("text", "attr", "continue"),
                Connect("attr", "count", "continue"),
                Connect("count", "shot", "continue"),
                Connect("shot", "return", "continue"),
            ]);
    }

    private static WorkflowNode Node(string id, string type, JsonObject parameters)
    {
        parameters["page"] = new JsonObject { ["$resource"] = new JsonObject { ["name"] = "page" } };
        return new(id, type, 1, parameters: parameters);
    }

    private static JsonObject Locator(string id)
    {
        return new JsonObject { ["$locator"] = new JsonObject { ["catalog"] = "smoke", ["version"] = "0.1.0", ["id"] = id } };
    }

    private static WorkflowConnection Connect(string source, string target, string sourcePort)
    {
        return new(new WorkflowEndpoint(source, sourcePort), new WorkflowEndpoint(target, "main"));
    }

    private static string HtmlUrl()
    {
        const string html = """
            <html><body>
            <label>Name <input id="name"></label>
            <select id="choice"><option value="a">A</option><option value="b">B</option></select>
            <label>Agree <input id="agree" type="checkbox"></label>
            <button id="submit" onclick="document.getElementById('output').style.display='block';document.getElementById('output').textContent='Hello '+document.getElementById('name').value;">Submit</button>
            <div id="output" data-testid="output" style="display:none"></div>
            <ul><li class="item" data-value="first">One</li><li class="item" data-value="second">Two</li><li class="item" data-value="third">Three</li></ul>
            </body></html>
            """;
        return "data:text/html," + Uri.EscapeDataString(html);
    }

    private static NodeExecutionResult OutputNode(WorkflowRuntimeResult result, string nodeId)
    {
        return result.NodeResults.Single(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));
    }

    private static string Output(WorkflowRuntimeResult result, string nodeId, string port)
    {
        return OutputNode(result, nodeId).Outputs[port]!.GetValue<string>();
    }
}
