using System.Text.Json.Nodes;
using SkeletonKey.Locators;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Web.Playwright;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.Advanced.Integration.Tests;

/// <summary>Contains the explicit opt-in Chromium network interception smoke.</summary>
public sealed class NetworkInterceptionChromiumTests
{
    /// <summary>Verifies synthetic responses and fail-closed unmatched requests.</summary>
    [Fact]
    public async Task ChromiumNetworkInterceptionSucceedsWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        const string interceptedUrl = "https://phase21.test/index.html";
        WorkflowResourceDefinition definition = new(
            StandardWorkflowResourceKinds.WebPage,
            capabilities: [StandardWorkflowResourceCapabilities.WebNavigation, StandardWorkflowResourceCapabilities.WebNetworkInterception],
            constraints: new JsonObject
            {
                ["engine"] = "chromium",
                ["visibility"] = "headless",
                ["profile"] = "ephemeral",
                ["network"] = new JsonObject
                {
                    ["defaultAction"] = "block",
                    ["maximumInterceptions"] = 16,
                    ["rules"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "document",
                            ["urlPattern"] = interceptedUrl,
                            ["action"] = "fulfill",
                            ["methods"] = new JsonArray("GET"),
                            ["resourceTypes"] = new JsonArray("document"),
                            ["status"] = 200,
                            ["contentType"] = "text/html; charset=utf-8",
                            ["body"] = "<html><body>phase-21-network</body></html>",
                            ["responseHeaders"] = new JsonObject { ["cache-control"] = "no-store" },
                        },
                    },
                },
            });
        PlaywrightPageResourceProvider provider = new();
        WorkflowRuntimeResourceRequest request = new("phase21-execution", "phase21-invocation", "phase21-workflow", "page", definition);
        await using IWorkflowRuntimeResourceInstance resource = await provider.CreateAsync(request);
        IWebPageAdapter adapter = resource.CreateHandle().GetRequiredAdapter<IWebPageAdapter>();

        string finalUrl = await adapter.NavigateAsync(new WebNavigationRequest(interceptedUrl));
        ResolvedLocatorPlan body = new("phase21", "0.1.0", "body", null, LocatorCardinality.One, [new ResolvedLocatorStrategy("css", selector: "body")]);
        IReadOnlyList<string?> text = await adapter.GetTextAsync(body);
        WebAutomationException blocked = await Assert.ThrowsAsync<WebAutomationException>(
            () => adapter.NavigateAsync(new WebNavigationRequest("https://phase21.test/blocked.html")).AsTask());

        Assert.Equal(interceptedUrl, finalUrl);
        Assert.Equal(["phase-21-network"], text);
        Assert.Equal(WebAutomationErrorCodes.NavigationFailed, blocked.Error.Code);
    }
}
