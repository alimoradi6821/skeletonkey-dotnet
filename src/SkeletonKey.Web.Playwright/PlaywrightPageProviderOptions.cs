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
    public PlaywrightPageProviderOptions(IWebNavigationPolicy? navigationPolicy = null, string testIdAttribute = "data-testid", IWorkflowArtifactStore? artifactStore = null)
    {
        NavigationPolicy = navigationPolicy ?? new DefaultWebNavigationPolicy();
        TestIdAttribute = string.IsNullOrWhiteSpace(testIdAttribute) ? "data-testid" : testIdAttribute;
        ArtifactStore = artifactStore;
    }

    /// <summary>Gets the navigation policy applied before page navigation.</summary>
    public IWebNavigationPolicy NavigationPolicy { get; }

    /// <summary>Gets the attribute used for test-id locator strategies.</summary>
    public string TestIdAttribute { get; }

    /// <summary>Gets the optional host-owned artifact store exposed to artifact-backed web handlers.</summary>
    public IWorkflowArtifactStore? ArtifactStore { get; }
}
