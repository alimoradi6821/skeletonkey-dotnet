using System.Text.Json.Nodes;
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
        Assert.Contains(StandardWorkflowResourceCapabilities.WebNavigation, provider.Capabilities);
        Assert.Contains(StandardWorkflowResourceCapabilities.WebScreenshot, provider.Capabilities);
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
}
