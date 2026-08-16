using SkeletonKey.Artifacts;
using SkeletonKey.Locators;

namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Defines provider-neutral browser page automation operations.
/// </summary>
public interface IWebPageAdapter
{
    /// <summary>Navigates the page and returns its final URL.</summary>
    public ValueTask<string> NavigateAsync(WebNavigationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Clicks one resolved locator target.</summary>
    public ValueTask ClickAsync(ResolvedLocatorPlan locator, WebClickRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fills one resolved locator target with a sensitive or ordinary string value.</summary>
    public ValueTask FillAsync(ResolvedLocatorPlan locator, WebFillRequest request, CancellationToken cancellationToken = default);

    /// <summary>Presses a key against one resolved locator target.</summary>
    public ValueTask PressAsync(ResolvedLocatorPlan locator, WebPressRequest request, CancellationToken cancellationToken = default);

    /// <summary>Selects option values against one resolved locator target.</summary>
    public ValueTask SelectOptionAsync(ResolvedLocatorPlan locator, WebSelectOptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sets checked state against one resolved locator target.</summary>
    public ValueTask SetCheckedAsync(ResolvedLocatorPlan locator, WebSetCheckedRequest request, CancellationToken cancellationToken = default);

    /// <summary>Waits for one resolved locator target state.</summary>
    public ValueTask WaitAsync(ResolvedLocatorPlan locator, WebWaitRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets text from resolved locator matches in DOM order.</summary>
    public ValueTask<IReadOnlyList<string?>> GetTextAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default);

    /// <summary>Gets attribute values from resolved locator matches in DOM order.</summary>
    public ValueTask<IReadOnlyList<string?>> GetAttributeAsync(ResolvedLocatorPlan locator, string name, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default);

    /// <summary>Gets the number of resolved locator matches.</summary>
    public ValueTask<int> GetCountAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default);

    /// <summary>Captures a page or target screenshot.</summary>
    public ValueTask<WebScreenshotResult> ScreenshotAsync(ResolvedLocatorPlan? locator, WebScreenshotRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens a new page in the existing browser context.</summary>
    public ValueTask<(WebPageReference Page, string Url)> OpenPageAsync(WebNavigationRequest request, bool activate = true, CancellationToken cancellationToken = default) => throw new NotSupportedException("Multiple page support is unavailable.");

    /// <summary>Lists known pages in creation order.</summary>
    public ValueTask<WebPageCollectionSnapshot> ListPagesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Multiple page support is unavailable.");

    /// <summary>Activates an existing page as the default operation target.</summary>
    public ValueTask ActivatePageAsync(WebPageReference page, CancellationToken cancellationToken = default) => throw new NotSupportedException("Multiple page support is unavailable.");

    /// <summary>Closes a known page.</summary>
    public ValueTask ClosePageAsync(WebPageReference page, CancellationToken cancellationToken = default) => throw new NotSupportedException("Multiple page support is unavailable.");

    /// <summary>Clicks and waits for a popup registered before the click.</summary>
    public ValueTask<(WebPageReference PopupPage, string Url)> ClickAndWaitForPopupAsync(ResolvedLocatorPlan locator, WebPopupRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException("Popup support is unavailable.");

    /// <summary>Uploads artifact-backed files to a file input.</summary>
    public ValueTask UploadFilesAsync(ResolvedLocatorPlan locator, WebUploadFilesRequest request, IWorkflowArtifactStore artifactStore, CancellationToken cancellationToken = default) => throw new NotSupportedException("Upload support is unavailable.");

    /// <summary>Clicks and waits for a download stored through the artifact store.</summary>
    public ValueTask<WorkflowArtifactReference> ClickAndWaitForDownloadAsync(ResolvedLocatorPlan locator, WebDownloadRequest request, IWorkflowArtifactStore artifactStore, CancellationToken cancellationToken = default) => throw new NotSupportedException("Download support is unavailable.");

    /// <summary>Clicks and waits for a browser dialog registered before the click.</summary>
    public ValueTask<WebDialogInformation> ClickAndWaitForDialogAsync(ResolvedLocatorPlan locator, WebDialogWaitRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException("Dialog support is unavailable.");

    /// <summary>Responds to a pending single-use dialog reference.</summary>
    public ValueTask RespondDialogAsync(WebDialogReference dialog, string action, string? promptText = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Dialog support is unavailable.");

    /// <summary>Gets cookies in deterministic order.</summary>
    public ValueTask<IReadOnlyList<WebCookie>> GetCookiesAsync(IReadOnlyList<string>? urls = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Cookie support is unavailable.");

    /// <summary>Sets explicit cookies.</summary>
    public ValueTask SetCookiesAsync(IReadOnlyList<WebCookie> cookies, CancellationToken cancellationToken = default) => throw new NotSupportedException("Cookie support is unavailable.");

    /// <summary>Clears cookies using bounded provider-neutral filters.</summary>
    public ValueTask ClearCookiesAsync(string? name = null, string? domain = null, string? path = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Cookie support is unavailable.");

    /// <summary>Exports storage state into a sensitive artifact.</summary>
    public ValueTask<WorkflowArtifactReference> ExportStorageStateAsync(IWorkflowArtifactStore artifactStore, WebStorageStateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException("Storage-state export is unavailable.");

    /// <summary>Imports storage state from a sensitive artifact.</summary>
    public ValueTask ImportStorageStateAsync(IWorkflowArtifactStore artifactStore, WorkflowArtifactReference artifact, CancellationToken cancellationToken = default) => throw new NotSupportedException("Storage-state import is unavailable.");

    /// <summary>Waits for the active or referenced page URL to match a value.</summary>
    public ValueTask WaitForUrlAsync(string url, WebTargetContext targetContext, int timeoutMilliseconds = 30000, CancellationToken cancellationToken = default) => throw new NotSupportedException("Advanced waits are unavailable.");

    /// <summary>Waits for a page load state.</summary>
    public ValueTask WaitForLoadStateAsync(WebNavigationWaitUntil state, WebTargetContext targetContext, int timeoutMilliseconds = 30000, CancellationToken cancellationToken = default) => throw new NotSupportedException("Advanced waits are unavailable.");
}
