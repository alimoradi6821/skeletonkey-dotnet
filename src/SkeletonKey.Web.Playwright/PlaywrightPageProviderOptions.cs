using SkeletonKey.Artifacts;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Playwright;

/// <summary>
/// Defines host-supplied options for Playwright page resource creation.
/// </summary>
public sealed class PlaywrightPageProviderOptions
{
    /// <summary>
    /// Initializes provider options.
    /// </summary>
    public PlaywrightPageProviderOptions(
        IWebNavigationPolicy? navigationPolicy = null,
        string testIdAttribute = "data-testid",
        IWorkflowArtifactStore? artifactStore = null,
        bool allowRemoteCdpEndpoints = false,
        int cdpConnectTimeoutMilliseconds = 30000,
        ManagedCdpBrowserHost? managedCdpBrowserHost = null)
    {
        if (cdpConnectTimeoutMilliseconds is <= 0 or > 300000)
        {
            throw new ArgumentOutOfRangeException(nameof(cdpConnectTimeoutMilliseconds), "CDP connect timeout must be between 1 and 300000 milliseconds.");
        }

        NavigationPolicy = navigationPolicy ?? new DefaultWebNavigationPolicy();
        TestIdAttribute = string.IsNullOrWhiteSpace(testIdAttribute) ? "data-testid" : testIdAttribute;
        ArtifactStore = artifactStore;
        AllowRemoteCdpEndpoints = allowRemoteCdpEndpoints;
        CdpConnectTimeoutMilliseconds = cdpConnectTimeoutMilliseconds;
        ManagedCdpBrowserHost = managedCdpBrowserHost;
    }

    /// <summary>Gets the navigation policy applied before page navigation.</summary>
    public IWebNavigationPolicy NavigationPolicy { get; }

    /// <summary>Gets the attribute used for test-id locator strategies.</summary>
    public string TestIdAttribute { get; }

    /// <summary>Gets the optional host-owned artifact store exposed to artifact-backed web handlers.</summary>
    public IWorkflowArtifactStore? ArtifactStore { get; }

    /// <summary>Gets whether workflows may attach to non-loopback CDP endpoints.</summary>
    public bool AllowRemoteCdpEndpoints { get; }

    /// <summary>Gets the bounded timeout used while establishing a CDP connection.</summary>
    public int CdpConnectTimeoutMilliseconds { get; }

    /// <summary>Gets the optional host-lifetime managed CDP browser owner.</summary>
    public ManagedCdpBrowserHost? ManagedCdpBrowserHost { get; }
}
