using System.Text.Json.Nodes;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Web.Playwright;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.Playwright.Tests;

/// <summary>
/// Covers Playwright provider configuration without launching browsers.
/// </summary>
public sealed class PlaywrightProviderConfigurationTests
{
    /// <summary>
    /// Verifies exact web.page kind and declared capabilities.
    /// </summary>
    [Fact]
    public void ProviderDeclaresWebPageKindAndCapabilities()
    {
        PlaywrightPageResourceProvider provider = new();

        Assert.Equal(StandardWorkflowResourceKinds.WebPage, provider.Kind);
        Assert.IsAssignableFrom<IWorkflowRuntimeResourceRecoveryProvider>(provider);
        Assert.Contains(StandardWorkflowResourceCapabilities.WebNavigation, provider.Capabilities);
        Assert.Contains(StandardWorkflowResourceCapabilities.WebScreenshot, provider.Capabilities);
        Assert.Contains(StandardWorkflowResourceCapabilities.WebNetworkInterception, provider.Capabilities);
    }

    /// <summary>
    /// Verifies supported browser engines parse.
    /// </summary>
    [Theory]
    [InlineData("chromium")]
    [InlineData("firefox")]
    [InlineData("webkit")]
    public void ConstraintsAcceptSupportedEngines(string engine)
    {
        var parsed = PlaywrightPageConstraints.Parse(new JsonObject { ["engine"] = engine });

        Assert.Equal(engine, parsed.Engine);
    }

    /// <summary>
    /// Verifies persistent profile requires an explicit directory.
    /// </summary>
    [Fact]
    public void PersistentProfileRequiresExplicitDirectory()
    {
        Assert.Throws<ArgumentException>(() => PlaywrightPageConstraints.Parse(new JsonObject { ["profile"] = "persistent" }));
    }

    /// <summary>
    /// Verifies unknown constraints are rejected.
    /// </summary>
    [Fact]
    public void UnknownConstraintsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => PlaywrightPageConstraints.Parse(new JsonObject { ["args"] = "--unsafe" }));
    }

    /// <summary>Verifies declarative network rules parse into an ordered bounded policy.</summary>
    [Fact]
    public void ConstraintsParseNetworkInterceptionPolicy()
    {
        var parsed = PlaywrightPageConstraints.Parse(new JsonObject
        {
            ["network"] = new JsonObject
            {
                ["defaultAction"] = "block",
                ["maximumInterceptions"] = 50,
                ["rules"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "mock-config",
                        ["urlPattern"] = "https://phase21.test/config",
                        ["action"] = "fulfill",
                        ["status"] = 200,
                        ["contentType"] = "application/json",
                        ["body"] = "{}",
                    },
                },
            },
            ["engine"] = "firefox",
        });

        Assert.NotNull(parsed.NetworkPolicy);
        Assert.Equal("firefox", parsed.Engine);
        Assert.Equal(WebNetworkInterceptionAction.Block, parsed.NetworkPolicy.DefaultAction);
        Assert.Equal(50, parsed.NetworkPolicy.MaximumInterceptions);
        Assert.Equal(WebNetworkInterceptionAction.Fulfill, Assert.Single(parsed.NetworkPolicy.Rules).Action);
    }

    /// <summary>Verifies modify rules parse request header set and removal operations.</summary>
    [Fact]
    public void ConstraintsParseNetworkHeaderModification()
    {
        var parsed = PlaywrightPageConstraints.Parse(new JsonObject
        {
            ["network"] = new JsonObject
            {
                ["rules"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "headers",
                        ["urlPattern"] = "https://phase21.test/*",
                        ["action"] = "modify",
                        ["methods"] = new JsonArray("GET"),
                        ["resourceTypes"] = new JsonArray("xhr"),
                        ["setRequestHeaders"] = new JsonObject { ["x-phase"] = "21" },
                        ["removeRequestHeaders"] = new JsonArray("referer"),
                    },
                },
            },
        });

        WebNetworkInterceptionRule rule = Assert.Single(parsed.NetworkPolicy!.Rules);
        Assert.Equal("21", rule.RequestHeaders["x-phase"]);
        Assert.Equal(["referer"], rule.RemovedRequestHeaders);
    }

    /// <summary>Verifies network policy and rule objects reject unknown properties.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConstraintsRejectUnknownNetworkProperties(bool onPolicy)
    {
        JsonObject network = onPolicy
            ? new JsonObject { ["unknown"] = true }
            : new JsonObject
            {
                ["rules"] = new JsonArray
                {
                    new JsonObject { ["id"] = "rule", ["urlPattern"] = "*", ["action"] = "allow", ["unknown"] = true },
                },
            };

        Assert.Throws<ArgumentException>(() => PlaywrightPageConstraints.Parse(new JsonObject { ["network"] = network }));
    }
    /// <summary>Verifies CDP attachment parses as an explicit Chromium-only connection mode.</summary>
    [Fact]
    public void ConstraintsAcceptCdpAttachment()
    {
        var parsed = PlaywrightPageConstraints.Parse(new JsonObject
        {
            ["engine"] = "chromium",
            ["connection"] = "cdp",
            ["cdpEndpoint"] = "http://127.0.0.1:9222",
            ["defaultTimeoutMilliseconds"] = 45000,
        });

        Assert.Equal("cdp", parsed.Connection);
        Assert.Equal("http://127.0.0.1:9222", parsed.CdpEndpoint);
        Assert.Equal(45000, parsed.DefaultTimeoutMilliseconds);
    }

    /// <summary>Verifies CDP attachment requires an explicit endpoint.</summary>
    [Fact]
    public void CdpAttachmentRequiresEndpoint()
    {
        Assert.Throws<ArgumentException>(() => PlaywrightPageConstraints.Parse(new JsonObject
        {
            ["connection"] = "cdp",
        }));
    }

    /// <summary>Verifies CDP attachment is Chromium-only.</summary>
    [Fact]
    public void CdpAttachmentRejectsNonChromiumEngines()
    {
        Assert.Throws<ArgumentException>(() => PlaywrightPageConstraints.Parse(new JsonObject
        {
            ["engine"] = "firefox",
            ["connection"] = "cdp",
            ["cdpEndpoint"] = "http://127.0.0.1:9222",
        }));
    }

    /// <summary>Verifies externally managed CDP browsers cannot receive launch-only constraints.</summary>
    [Theory]
    [InlineData("channel")]
    [InlineData("visibility")]
    [InlineData("profile")]
    [InlineData("userDataDirectory")]
    [InlineData("viewportWidth")]
    [InlineData("viewportHeight")]
    [InlineData("locale")]
    [InlineData("userAgent")]
    public void CdpAttachmentRejectsLaunchOnlyConstraints(string property)
    {
        JsonObject constraints = new()
        {
            ["connection"] = "cdp",
            ["cdpEndpoint"] = "http://127.0.0.1:9222",
        };
        constraints[property] = property switch
        {
            "viewportWidth" or "viewportHeight" => 1024,
            "visibility" => "headful",
            "profile" => "persistent",
            "userDataDirectory" => "profile",
            _ => "value",
        };

        Assert.Throws<ArgumentException>(() => PlaywrightPageConstraints.Parse(constraints));
    }

    /// <summary>Verifies CDP endpoints use supported endpoint schemes.</summary>
    [Theory]
    [InlineData("file:///tmp/devtools")]
    [InlineData("not-a-url")]
    public void CdpAttachmentRejectsUnsupportedEndpoints(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => PlaywrightPageConstraints.Parse(new JsonObject
        {
            ["connection"] = "cdp",
            ["cdpEndpoint"] = endpoint,
        }));
    }

    /// <summary>Verifies remote CDP is denied by default and connection timeout is bounded.</summary>
    [Fact]
    public void ProviderOptionsUseSafeCdpDefaults()
    {
        PlaywrightPageProviderOptions options = new();

        Assert.False(options.AllowRemoteCdpEndpoints);
        Assert.Equal(30000, options.CdpConnectTimeoutMilliseconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaywrightPageProviderOptions(cdpConnectTimeoutMilliseconds: 300001));
    }

}
