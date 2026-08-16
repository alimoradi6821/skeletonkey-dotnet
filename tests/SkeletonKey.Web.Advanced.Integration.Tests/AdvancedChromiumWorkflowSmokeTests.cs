using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.Artifacts;
using SkeletonKey.Artifacts.FileSystem;
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

namespace SkeletonKey.Web.Advanced.Integration.Tests;

/// <summary>
/// Covers opt-in advanced Chromium behavior through the normal Workflow pipeline.
/// </summary>
public sealed class AdvancedChromiumWorkflowSmokeTests
{
    /// <summary>
    /// Verifies nested-frame upload and download use Workflow JSON, Locator slots, handlers, and Playwright.
    /// </summary>
    [Fact]
    public async Task NestedFrameUploadAndDownloadWorkflowSucceedsWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        string artifactRoot = Path.Combine(Path.GetTempPath(), "skeletonkey-advanced-smoke-" + Guid.NewGuid().ToString("N"));
        FileSystemWorkflowArtifactStore artifactStore = new(new FileSystemArtifactStoreOptions(artifactRoot, maximumArtifactBytes: 1024 * 1024));
        WorkflowArtifactReference uploadArtifact = await artifactStore.WriteAsync(
            new WorkflowArtifactWriteRequest("upload-note.txt", "text/plain", WorkflowArtifactSensitivity.Internal, 1024),
            new MemoryStream(Encoding.UTF8.GetBytes("upload-body")));

        LocatorDocument locators = new(
            id: "advanced-smoke",
            locators: new Dictionary<string, LocatorDefinition>(StringComparer.Ordinal)
            {
                ["outer"] = new(strategies: [new("css", selector: "iframe#outer")]),
                ["inner"] = new(strategies: [new("css", selector: "iframe#inner")]),
                ["upload"] = new(strategies: [new("css", selector: "#upload")]),
                ["uploadOutput"] = new(strategies: [new("css", selector: "#upload-output")]),
                ["download"] = new(strategies: [new("css", selector: "#download")]),
            });
        LocatorPlanResolver resolver = new(new ImmutableLocatorDocumentRepository([locators]));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. WebBuiltInWorkflowNodeCatalog.Catalog.Definitions]);
        IReadOnlyList<INodeHandler> handlers = [.. BuiltInRuntimeHandlers.Create(), .. WebBuiltInRuntimeHandlers.Create()];
        WorkflowDocument workflow = Workflow(uploadArtifact);
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
            options: new WorkflowRuntimeOptions(maximumExecutedNodeAttempts: 100),
            resourceProviders: [new PlaywrightPageResourceProvider(new PlaywrightPageProviderOptions(artifactStore: artifactStore))],
            locatorResolver: resolver);

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(new WorkflowExecutionRequest(workflow, "advanced-smoke-execution", "advanced-smoke-plan"));

        Assert.True(
            result.Result.Status == WorkflowExecutionStatus.Succeeded,
            FailureMessage(result));
        Assert.Equal("upload-note.txt:upload-body", Output(result, "uploadText", "result"));
        WorkflowArtifactReference download = Artifact(OutputNode(result, "download").Outputs["artifact"]!);
        Assert.Equal("_CON.txt", download.Filename);
        Assert.Equal("download-body", await ReadArtifactAsync(artifactStore, download));
        Assert.Equal(Sha256("download-body"), download.Sha256);
        Assert.Equal("done", result.Result.Outcome!.Code);
    }

    private static WorkflowDocument Workflow(WorkflowArtifactReference uploadArtifact)
    {
        return new(
            id: "advanced-web-smoke",
            name: "Advanced Web Smoke",
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
                        StandardWorkflowResourceCapabilities.WebForms,
                    ],
                    constraints: new JsonObject { ["engine"] = "chromium", ["visibility"] = "headless", ["profile"] = "ephemeral", ["defaultTimeoutMilliseconds"] = 10000 }),
            },
            nodes:
            [
                new("start", "core.start", 1),
                Node("navigate", "web.navigate", new JsonObject { ["url"] = HtmlUrl() }),
                Node("upload", "web.uploadFiles", FrameParameters("upload", new JsonObject
                {
                    ["artifacts"] = new JsonArray(ArtifactJson(uploadArtifact)),
                    ["maximumFiles"] = 1,
                    ["maximumAggregateBytes"] = 1024,
                })),
                Node("uploadText", "web.getText", FrameParameters("uploadOutput")),
                Node("download", "web.clickAndWaitForDownload", FrameParameters("download", new JsonObject { ["maximumBytes"] = 1024 })),
                new("return", "core.return", 1, parameters: new JsonObject { ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" } }),
            ],
            connections:
            [
                Connect("start", "navigate", "main"),
                Connect("navigate", "upload", "continue"),
                Connect("upload", "uploadText", "continue"),
                Connect("uploadText", "download", "continue"),
                Connect("download", "return", "continue"),
            ]);
    }

    private static WorkflowNode Node(string id, string type, JsonObject parameters)
    {
        parameters["page"] = new JsonObject { ["$resource"] = new JsonObject { ["name"] = "page" } };
        return new(id, type, 1, parameters: parameters);
    }

    private static JsonObject FrameParameters(string target, JsonObject? parameters = null)
    {
        JsonObject result = parameters ?? [];
        result["target"] = Locator(target);
        result["frame1"] = Locator("outer");
        result["frame2"] = Locator("inner");
        result["frames"] = new JsonArray("frame1", "frame2");
        return result;
    }

    private static JsonObject Locator(string id)
    {
        return new JsonObject { ["$locator"] = new JsonObject { ["catalog"] = "advanced-smoke", ["version"] = "0.1.0", ["id"] = id } };
    }

    private static JsonObject ArtifactJson(WorkflowArtifactReference artifact)
    {
        return new JsonObject
        {
            ["artifactId"] = artifact.ArtifactId,
            ["filename"] = artifact.Filename,
            ["mediaType"] = artifact.MediaType,
            ["size"] = artifact.Size,
            ["sensitivity"] = artifact.Sensitivity.ToString(),
            ["sha256"] = artifact.Sha256,
        };
    }

    private static WorkflowArtifactReference Artifact(JsonNode node)
    {
        JsonObject artifact = node.AsObject();
        return new(
            artifact["artifactId"]!.GetValue<string>(),
            artifact["filename"]!.GetValue<string>(),
            artifact["mediaType"]!.GetValue<string>(),
            artifact["size"]!.GetValue<long>(),
            Enum.Parse<WorkflowArtifactSensitivity>(artifact["sensitivity"]!.GetValue<string>()),
            artifact["sha256"]?.GetValue<string>());
    }

    private static WorkflowConnection Connect(string source, string target, string sourcePort)
    {
        return new(new WorkflowEndpoint(source, sourcePort), new WorkflowEndpoint(target, "main"));
    }

    private static string HtmlUrl()
    {
        string inner = """
            <html><body>
            <input id="upload" type="file">
            <div id="upload-output"></div>
            <a id="download" download="CON.txt" href="data:text/plain,download-body">Download</a>
            <script>
            document.getElementById('upload').addEventListener('change', async event => {
              const file = event.target.files[0];
              document.getElementById('upload-output').textContent = file.name + ':' + await file.text();
            });
            </script>
            </body></html>
            """;
        string outer = "<html><body><iframe id=\"inner\" src=\"" + DataUrl(inner) + "\"></iframe></body></html>";
        string root = "<html><body><iframe id=\"outer\" src=\"" + DataUrl(outer) + "\"></iframe></body></html>";
        return DataUrl(root);
    }

    private static string DataUrl(string html)
    {
        return "data:text/html," + Uri.EscapeDataString(html);
    }

    private static NodeExecutionResult OutputNode(WorkflowRuntimeResult result, string nodeId)
    {
        return result.NodeResults.Single(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));
    }

    private static string FailureMessage(WorkflowRuntimeResult result)
    {
        if (result.Result.Error is null)
        {
            return string.Join(Environment.NewLine, result.NodeResults.Select(static node => $"{node.NodeId}: {node.Status}"));
        }

        return string.Join(
            Environment.NewLine,
            [$"{result.Result.Error.Code}: {result.Result.Error.Message} ({result.Result.Error.NodeId})", .. result.NodeResults.Select(static node => $"{node.NodeId}: {node.Status} {node.Error?.Code} {node.Error?.Message}")]);
    }

    private static string Output(WorkflowRuntimeResult result, string nodeId, string port)
    {
        return OutputNode(result, nodeId).Outputs[port]!.GetValue<string>();
    }

    private static async Task<string> ReadArtifactAsync(IWorkflowArtifactStore store, WorkflowArtifactReference artifact)
    {
        await using Stream stream = await store.OpenReadAsync(artifact);
        using StreamReader reader = new(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
