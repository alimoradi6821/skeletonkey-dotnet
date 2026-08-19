using Microsoft.Playwright;
using SkeletonKey.Artifacts;
using SkeletonKey.Locators;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Playwright;

/// <summary>
/// Implements provider-neutral web page automation using a private Playwright page.
/// </summary>
public sealed class PlaywrightPageAdapter : IWebPageAdapter
{
    private const string _primaryPageId = "primary";
    private readonly IBrowser? _browser;
    private readonly BrowserNewContextOptions _contextOptions;
    private readonly Dictionary<string, PageSlot> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DialogSlot> _dialogs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stalePageIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _staleDialogIds = new(StringComparer.Ordinal);
    private readonly List<string> _temporaryUploadDirectories = [];
    private readonly IWebNavigationPolicy _navigationPolicy;
    private readonly PlaywrightNetworkInterceptor? _networkInterceptor;
    private readonly string _testIdAttribute;
    private readonly int _defaultTimeoutMilliseconds;
    private IBrowserContext _context;
    private string _activePageId = _primaryPageId;
    private int _nextPageNumber = 2;
    private int _nextDialogNumber = 1;
    private int _contextGeneration = 1;

    /// <summary>
    /// Initializes a Playwright page adapter.
    /// </summary>
    public PlaywrightPageAdapter(IBrowser? browser, IBrowserContext context, BrowserNewContextOptions contextOptions, IPage page, IWebNavigationPolicy navigationPolicy, string testIdAttribute, int defaultTimeoutMilliseconds)
        : this(browser, context, contextOptions, page, navigationPolicy, testIdAttribute, defaultTimeoutMilliseconds, null)
    {
    }

    internal PlaywrightPageAdapter(IBrowser? browser, IBrowserContext context, BrowserNewContextOptions contextOptions, IPage page, IWebNavigationPolicy navigationPolicy, string testIdAttribute, int defaultTimeoutMilliseconds, PlaywrightNetworkInterceptor? networkInterceptor)
    {
        _browser = browser;
        _context = context;
        _contextOptions = contextOptions;
        _navigationPolicy = navigationPolicy;
        _networkInterceptor = networkInterceptor;
        _testIdAttribute = testIdAttribute;
        _defaultTimeoutMilliseconds = defaultTimeoutMilliseconds;
        _pages[_primaryPageId] = new PageSlot(_primaryPageId, page, _contextGeneration);
    }

    /// <summary>Disposes the currently owned browser context.</summary>
    internal async ValueTask DisposeAsync()
    {
        foreach (DialogSlot dialog in _dialogs.Values)
        {
            try
            {
                await dialog.Dialog.DismissAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }

        _dialogs.Clear();
        try
        {
            await _context.CloseAsync().ConfigureAwait(false);
        }
        finally
        {
            foreach (string directory in _temporaryUploadDirectories)
            {
                TryDeleteDirectory(directory);
            }

            _temporaryUploadDirectories.Clear();
        }
    }

    /// <summary>Captures reconstructable browser-context state at a runtime safe boundary.</summary>
    internal async ValueTask<WorkflowRuntimeResourceCheckpointState?> CaptureCheckpointStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_browser is null ||
            _dialogs.Count > 0 ||
            _pages.Count > PlaywrightPageCheckpointState.MaximumPages ||
            _stalePageIds.Count > PlaywrightPageCheckpointState.MaximumPages ||
            _staleDialogIds.Count > PlaywrightPageCheckpointState.MaximumPages)
        {
            return null;
        }

        string storageState = await _context.StorageStateAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (System.Text.Encoding.UTF8.GetByteCount(storageState) > PlaywrightPageCheckpointState.MaximumStorageStateBytes ||
            _pages.Values.Any(static page => !page.IsClosed && (page.Page.Url.Length > PlaywrightPageCheckpointState.MaximumUrlLength || !Uri.TryCreate(page.Page.Url, UriKind.Absolute, out _))))
        {
            return null;
        }

        PlaywrightCheckpointPage[] pages = _pages.Values
            .OrderBy(static page => page.Order, StringComparer.Ordinal)
            .Select(static page => new PlaywrightCheckpointPage(page.Id, page.IsClosed ? "about:blank" : page.Page.Url, page.IsClosed))
            .ToArray();
        return new PlaywrightPageCheckpointState(
            storageState,
            _activePageId,
            _nextPageNumber,
            _nextDialogNumber,
            pages,
            _stalePageIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
            _staleDialogIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray()).ToResourceState();
    }

    /// <summary>Restores page identities and URLs after storage state has been applied to a new context.</summary>
    internal async ValueTask RestoreCheckpointStateAsync(PlaywrightPageCheckpointState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        if (_pages.Count != 1 || !_pages.TryGetValue(_primaryPageId, out PageSlot? initialPrimary))
        {
            throw new InvalidOperationException("Playwright recovery requires a fresh browser context.");
        }

        _pages.Clear();
        _dialogs.Clear();
        _stalePageIds.Clear();
        _stalePageIds.UnionWith(state.StalePageIds);
        _staleDialogIds.Clear();
        _staleDialogIds.UnionWith(state.StaleDialogIds);
        _contextGeneration = 1;
        _nextPageNumber = state.NextPageNumber;
        _nextDialogNumber = state.NextDialogNumber;

        bool primaryOpen = false;
        foreach (PlaywrightCheckpointPage saved in state.Pages)
        {
            if (saved.IsClosed)
            {
                _pages[saved.Id] = new PageSlot(saved.Id, initialPrimary.Page, _contextGeneration, IsClosed: true);
                continue;
            }

            IPage page = string.Equals(saved.Id, _primaryPageId, StringComparison.Ordinal)
                ? initialPrimary.Page
                : await _context.NewPageAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            primaryOpen |= string.Equals(saved.Id, _primaryPageId, StringComparison.Ordinal);
            WebOperationError? policyError = _navigationPolicy.ValidateNavigation(saved.Url);
            if (policyError is not null)
            {
                throw new WebAutomationException(policyError);
            }

            if (!string.Equals(saved.Url, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                IResponse? response = await page.GotoAsync(saved.Url, new PageGotoOptions
                {
                    Timeout = BoundedTimeout(_defaultTimeoutMilliseconds),
                    WaitUntil = WaitUntilState.Load,
                }).WaitAsync(cancellationToken).ConfigureAwait(false);
                if (response is not null && !response.Ok)
                {
                    throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.NavigationFailed, "Checkpoint page reconstruction navigation failed.", "restore"));
                }
            }

            _pages[saved.Id] = new PageSlot(saved.Id, page, _contextGeneration);
        }

        if (!primaryOpen)
        {
            await initialPrimary.Page.CloseAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        _activePageId = state.ActivePageId;
    }

    /// <inheritdoc />
    public async ValueTask<string> NavigateAsync(WebNavigationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WebOperationError? policyError = _navigationPolicy.ValidateNavigation(request.Url);
        if (policyError is not null)
        {
            throw new WebAutomationException(policyError);
        }

        try
        {
            IPage page = ActivePage();
            IResponse? response = await page.GotoAsync(request.Url, new PageGotoOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds), WaitUntil = MapWait(request.WaitUntil) }).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (response is not null && !response.Ok)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.NavigationFailed, "Navigation failed.", "navigate"));
            }

            return page.Url;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.LocatorOperationTimeout, "Navigation timed out.", "navigate"), exception);
        }
        catch (PlaywrightException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.NavigationFailed, "Navigation failed.", "navigate"), exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask ClickAsync(ResolvedLocatorPlan locator, WebClickRequest request, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "click", cancellationToken, request.TargetContext).ConfigureAwait(false);
        await RunActionAsync(async () => await target.ClickAsync(new LocatorClickOptions { Button = MapButton(request.Button), ClickCount = request.ClickCount, Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false), "click", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask FillAsync(ResolvedLocatorPlan locator, WebFillRequest request, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "fill", cancellationToken, request.TargetContext).ConfigureAwait(false);
        await RunActionAsync(async () => await target.FillAsync(request.Value, new LocatorFillOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false), "fill", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PressAsync(ResolvedLocatorPlan locator, WebPressRequest request, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "press", cancellationToken, request.TargetContext).ConfigureAwait(false);
        await RunActionAsync(async () => await target.PressAsync(request.Key, new LocatorPressOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false), "press", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SelectOptionAsync(ResolvedLocatorPlan locator, WebSelectOptionRequest request, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "selectOption", cancellationToken, request.TargetContext).ConfigureAwait(false);
        await RunActionAsync(async () => await target.SelectOptionAsync([.. request.Values], new LocatorSelectOptionOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false), "selectOption", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetCheckedAsync(ResolvedLocatorPlan locator, WebSetCheckedRequest request, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "setChecked", cancellationToken, request.TargetContext).ConfigureAwait(false);
        await RunActionAsync(async () => await target.SetCheckedAsync(request.Checked, new LocatorSetCheckedOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false), "setChecked", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask WaitAsync(ResolvedLocatorPlan locator, WebWaitRequest request, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "wait", cancellationToken, request.TargetContext).ConfigureAwait(false);
        await RunActionAsync(async () => await target.WaitForAsync(new LocatorWaitForOptions { State = MapWaitState(request.State), Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false), "wait", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string?>> GetTextAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveCollectionAsync(locator, timeoutMilliseconds, "getText", cancellationToken, targetContext).ConfigureAwait(false);
        int count = await target.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        List<string?> values = [];
        for (int index = 0; index < count; index++)
        {
            values.Add(await target.Nth(index).TextContentAsync(new LocatorTextContentOptions { Timeout = BoundedTimeout(timeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false));
        }

        return values;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string?>> GetAttributeAsync(ResolvedLocatorPlan locator, string name, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveCollectionAsync(locator, timeoutMilliseconds, "getAttribute", cancellationToken, targetContext).ConfigureAwait(false);
        int count = await target.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        List<string?> values = [];
        for (int index = 0; index < count; index++)
        {
            values.Add(await target.Nth(index).GetAttributeAsync(name, new LocatorGetAttributeOptions { Timeout = BoundedTimeout(timeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false));
        }

        return values;
    }

    /// <inheritdoc />
    public async ValueTask<int> GetCountAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds = 30000, WebTargetContext? targetContext = null, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveCollectionAsync(locator, timeoutMilliseconds, "getCount", cancellationToken, targetContext).ConfigureAwait(false);
        return await target.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<WebScreenshotResult> ScreenshotAsync(ResolvedLocatorPlan? locator, WebScreenshotRequest request, CancellationToken cancellationToken = default)
    {
        byte[] bytes = locator is null
            ? await ActivePage().ScreenshotAsync(new PageScreenshotOptions { Type = MapScreenshotType(request.Format), Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false)
            : await (await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "screenshot", cancellationToken, request.TargetContext).ConfigureAwait(false)).ScreenshotAsync(new LocatorScreenshotOptions { Type = MapScreenshotType(request.Format), Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false);

        if (bytes.Length > request.MaximumBytes)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.ScreenshotSizeLimitExceeded, "Screenshot size limit exceeded.", "screenshot"));
        }

        return new WebScreenshotResult(request.Format == WebScreenshotFormat.Jpeg ? "image/jpeg" : "image/png", bytes);
    }

    /// <inheritdoc />
    public async ValueTask<(WebPageReference Page, string Url)> OpenPageAsync(WebNavigationRequest request, bool activate = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WebOperationError? policyError = _navigationPolicy.ValidateNavigation(request.Url);
        if (policyError is not null)
        {
            throw new WebAutomationException(policyError);
        }

        IPage page = await _context.NewPageAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        string pageId = "page-" + _nextPageNumber++;
        _pages[pageId] = new PageSlot(pageId, page, _contextGeneration);
        try
        {
            IResponse? response = await page.GotoAsync(request.Url, new PageGotoOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds), WaitUntil = MapWait(request.WaitUntil) }).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (response is not null && !response.Ok)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.NavigationFailed, "Navigation failed.", "openPage"));
            }

            if (activate)
            {
                _activePageId = pageId;
            }

            return (new WebPageReference(pageId), page.Url);
        }
        catch
        {
            _pages[pageId] = _pages[pageId] with { IsClosed = true };
            await page.CloseAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<WebPageCollectionSnapshot> ListPagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<WebPageInformation> pages = [];
        foreach (PageSlot slot in _pages.Values.OrderBy(static slot => slot.Order, StringComparer.Ordinal))
        {
            string? title = slot.IsClosed ? null : await slot.Page.TitleAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            pages.Add(new WebPageInformation(new WebPageReference(slot.Id), slot.IsClosed ? string.Empty : slot.Page.Url, title, string.Equals(slot.Id, _activePageId, StringComparison.Ordinal), slot.IsClosed));
        }

        return new WebPageCollectionSnapshot(pages);
    }

    /// <inheritdoc />
    public ValueTask ActivatePageAsync(WebPageReference page, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = PageFor(page, failOnClosed: true);
        _activePageId = page.PageId;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask ClosePageAsync(WebPageReference page, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PageSlot slot = SlotFor(page, failOnClosed: true);
        if (string.Equals(page.PageId, _primaryPageId, StringComparison.Ordinal) && _pages.Values.Count(static candidate => !candidate.IsClosed) < 2)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.PageAlreadyClosed, "Primary page cannot be closed while it is the only open page.", "closePage"));
        }

        await slot.Page.CloseAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        _pages[page.PageId] = slot with { IsClosed = true };
        if (string.Equals(_activePageId, page.PageId, StringComparison.Ordinal))
        {
            _activePageId = _pages.Values.OrderBy(static candidate => candidate.Order, StringComparer.Ordinal).First(static candidate => !candidate.IsClosed).Id;
        }
    }

    /// <inheritdoc />
    public async ValueTask<(WebPageReference PopupPage, string Url)> ClickAndWaitForPopupAsync(ResolvedLocatorPlan locator, WebPopupRequest request, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "clickAndWaitForPopup", cancellationToken, request.TargetContext).ConfigureAwait(false);
        IPage parent = ActivePage();
        IPage popup = await parent.RunAndWaitForPopupAsync(async () =>
            await target.ClickAsync(new LocatorClickOptions { Button = MapButton(request.Button), ClickCount = request.ClickCount, Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false),
            new PageRunAndWaitForPopupOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false);
        string pageId = "page-" + _nextPageNumber++;
        _pages[pageId] = new PageSlot(pageId, popup, _contextGeneration);
        if (request.ActivatePopup)
        {
            _activePageId = pageId;
        }

        return (new WebPageReference(pageId), popup.Url);
    }

    /// <inheritdoc />
    public async ValueTask UploadFilesAsync(ResolvedLocatorPlan locator, WebUploadFilesRequest request, IWorkflowArtifactStore artifactStore, CancellationToken cancellationToken = default)
    {
        if (request.Artifacts.Count == 0 || request.Artifacts.Count > request.MaximumFiles)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.UploadFailed, "Upload artifact count is outside the configured limit.", "uploadFiles"));
        }

        List<WorkflowArtifactMetadata> metadata = [];
        long aggregateBytes = 0;
        try
        {
            foreach (WorkflowArtifactReference artifact in request.Artifacts)
            {
                WorkflowArtifactMetadata item = await artifactStore.GetMetadataAsync(artifact, cancellationToken).ConfigureAwait(false);
                aggregateBytes += item.Reference.Size;
                if (aggregateBytes > request.MaximumAggregateBytes)
                {
                    throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.UploadFailed, "Upload aggregate size exceeds the configured limit.", "uploadFiles"));
                }

                metadata.Add(item);
            }
        }
        catch (WorkflowArtifactException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.ArtifactUnavailable, "Upload artifact is unavailable.", "uploadFiles"), exception);
        }

        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "uploadFiles", cancellationToken, request.TargetContext).ConfigureAwait(false);
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "skeletonkey-upload-" + Guid.NewGuid().ToString("N"));
        List<string> temporaryFiles = [];
        bool retainTemporaryDirectory = false;
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            foreach (WorkflowArtifactMetadata item in metadata)
            {
                await using Stream source = await artifactStore.OpenReadAsync(item.Reference, cancellationToken).ConfigureAwait(false);
                string itemDirectory = Path.Combine(temporaryDirectory, temporaryFiles.Count.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(itemDirectory);
                string temporaryPath = Path.Combine(itemDirectory, SafeUploadFilename(item.Reference.Filename));
                await using FileStream destination = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                temporaryFiles.Add(temporaryPath);
            }

            await target.SetInputFilesAsync([.. temporaryFiles], new LocatorSetInputFilesOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false);
            _temporaryUploadDirectories.Add(temporaryDirectory);
            retainTemporaryDirectory = true;
        }
        catch (WorkflowArtifactException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.ArtifactUnavailable, "Upload artifact is unavailable.", "uploadFiles"), exception);
        }
        catch (PlaywrightException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.UploadFailed, "Upload operation failed.", "uploadFiles"), exception);
        }
        finally
        {
            if (!retainTemporaryDirectory)
            {
                TryDeleteDirectory(temporaryDirectory);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowArtifactReference> ClickAndWaitForDownloadAsync(ResolvedLocatorPlan locator, WebDownloadRequest request, IWorkflowArtifactStore artifactStore, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "clickAndWaitForDownload", cancellationToken, request.TargetContext).ConfigureAwait(false);
        IDownload download;
        try
        {
            download = await ActivePage().RunAndWaitForDownloadAsync(async () =>
                await target.ClickAsync(new LocatorClickOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false),
                new PageRunAndWaitForDownloadOptions { Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.DownloadTimeout, "Download did not begin before timeout.", "clickAndWaitForDownload"), exception);
        }
        catch (PlaywrightException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.DownloadPersistenceFailed, "Download failed.", "clickAndWaitForDownload"), exception);
        }

        try
        {
            await using Stream input = await download.CreateReadStreamAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return await artifactStore.WriteAsync(new WorkflowArtifactWriteRequest(SafeDownloadFilename(download.SuggestedFilename), "application/octet-stream", request.Sensitivity, request.MaximumBytes), input, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkflowArtifactException exception) when (string.Equals(exception.Code, WorkflowArtifactErrorCodes.ArtifactSizeLimitExceeded, StringComparison.Ordinal))
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.DownloadSizeLimitExceeded, "Download exceeded the configured maximum size.", "clickAndWaitForDownload"), exception);
        }
        catch (WorkflowArtifactException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.DownloadPersistenceFailed, "Download persistence failed.", "clickAndWaitForDownload"), exception);
        }
        catch (PlaywrightException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.DownloadPersistenceFailed, "Download stream could not be read.", "clickAndWaitForDownload"), exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WebDialogInformation> ClickAndWaitForDialogAsync(ResolvedLocatorPlan locator, WebDialogWaitRequest request, CancellationToken cancellationToken = default)
    {
        ILocator target = await ResolveSingleAsync(locator, request.TimeoutMilliseconds, request.ElementIndex, "clickAndWaitForDialog", cancellationToken, request.TargetContext).ConfigureAwait(false);
        IDialog? dialog = null;
        void CaptureDialog(object? _, IDialog captured)
        {
            dialog = captured;
        }

        IPage page = ActivePage();
        page.Dialog += CaptureDialog;
        try
        {
            await RunActionAsync(async () =>
                await target.ClickAsync(new LocatorClickOptions { Button = MapButton(request.Button), ClickCount = request.ClickCount, Timeout = BoundedTimeout(request.TimeoutMilliseconds) }).ConfigureAwait(false),
                "clickAndWaitForDialog",
                cancellationToken).ConfigureAwait(false);

            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(BoundedTimeout(request.TimeoutMilliseconds));
            while (dialog is null && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            page.Dialog -= CaptureDialog;
        }

        if (dialog is null)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.DialogTimeout, "Dialog did not appear before timeout.", "clickAndWaitForDialog"));
        }

        string dialogId = "dialog-" + _nextDialogNumber++;
        _dialogs[dialogId] = new DialogSlot(dialog, _contextGeneration);
        return new WebDialogInformation(new WebDialogReference(dialogId), MapDialogKind(dialog.Type), dialog.Message);
    }

    /// <inheritdoc />
    public async ValueTask RespondDialogAsync(WebDialogReference dialog, string action, string? promptText = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_staleDialogIds.Contains(dialog.DialogId))
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StaleBrowsingContextReference, "Dialog reference belongs to a previous browser context.", "respondDialog"));
        }

        if (!_dialogs.Remove(dialog.DialogId, out DialogSlot? found))
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.UnknownDialogReference, "Unknown or stale dialog reference.", "respondDialog"));
        }

        if (found.Generation != _contextGeneration)
        {
            _staleDialogIds.Add(dialog.DialogId);
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StaleBrowsingContextReference, "Dialog reference belongs to a previous browser context.", "respondDialog"));
        }

        if (string.Equals(action, "accept", StringComparison.Ordinal))
        {
            await found.Dialog.AcceptAsync(promptText).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (promptText is not null)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.InvalidDialogResponse, "Prompt text is only allowed when accepting a prompt.", "respondDialog"));
        }

        await found.Dialog.DismissAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebCookie>> GetCookiesAsync(IReadOnlyList<string>? urls = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<BrowserContextCookiesResult> cookies = urls is null
            ? await _context.CookiesAsync().WaitAsync(cancellationToken).ConfigureAwait(false)
            : await _context.CookiesAsync(urls).WaitAsync(cancellationToken).ConfigureAwait(false);
        return cookies.OrderBy(static cookie => cookie.Domain, StringComparer.Ordinal)
            .ThenBy(static cookie => cookie.Path, StringComparer.Ordinal)
            .ThenBy(static cookie => cookie.Name, StringComparer.Ordinal)
            .Select(static cookie => new WebCookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path, cookie.Expires, cookie.SameSite.ToString(), cookie.HttpOnly, cookie.Secure))
            .ToArray();
    }

    /// <inheritdoc />
    public async ValueTask SetCookiesAsync(IReadOnlyList<WebCookie> cookies, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _context.AddCookiesAsync(cookies.Select(static cookie => new Cookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = cookie.Domain,
            Path = cookie.Path,
            Expires = cookie.Expires.HasValue ? (float)cookie.Expires.Value : null,
            HttpOnly = cookie.HttpOnly,
            Secure = cookie.Secure,
            SameSite = MapSameSite(cookie.SameSite),
        })).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ClearCookiesAsync(string? name = null, string? domain = null, string? path = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Name = name, Domain = domain, Path = path }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowArtifactReference> ExportStorageStateAsync(IWorkflowArtifactStore artifactStore, WebStorageStateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            string json = await _context.StorageStateAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            await using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(json));
            return await artifactStore.WriteAsync(new WorkflowArtifactWriteRequest("storage-state.json", "application/json", WorkflowArtifactSensitivity.Sensitive, request.MaximumBytes), stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StorageStateExportFailed, "Storage-state export failed.", "exportStorageState"), exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask ImportStorageStateAsync(IWorkflowArtifactStore artifactStore, WorkflowArtifactReference artifact, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_browser is null)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StorageStateImportFailed, "Storage-state import requires an ephemeral browser context.", "importStorageState"));
        }

        WorkflowArtifactMetadata metadata = await artifactStore.GetMetadataAsync(artifact, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(metadata.Reference.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) || metadata.Reference.Size > 4 * 1024 * 1024)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StorageStateImportFailed, "Storage-state artifact metadata is invalid.", "importStorageState"));
        }

        string storageState;
        await using (Stream input = await artifactStore.OpenReadAsync(artifact, cancellationToken).ConfigureAwait(false))
        {
            using MemoryStream buffer = new();
            await input.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (buffer.Length > 4 * 1024 * 1024)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StorageStateImportFailed, "Storage-state artifact exceeds the maximum size.", "importStorageState"));
            }

            storageState = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }

        IBrowserContext newContext;
        try
        {
            BrowserNewContextOptions options = CloneContextOptions(_contextOptions);
            options.StorageState = storageState;
            newContext = await _browser.NewContextAsync(options).WaitAsync(cancellationToken).ConfigureAwait(false);
            newContext.SetDefaultTimeout(_defaultTimeoutMilliseconds);
            if (_networkInterceptor is not null)
            {
                await _networkInterceptor.AttachAsync(newContext, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (PlaywrightException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StorageStateImportFailed, "Storage-state import failed.", "importStorageState"), exception);
        }

        IPage? newPage = null;
        try
        {
            newPage = await newContext.NewPageAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            IBrowserContext oldContext = _context;
            foreach (PageSlot slot in _pages.Values)
            {
                _stalePageIds.Add(slot.Id);
            }

            foreach (string dialogId in _dialogs.Keys)
            {
                _staleDialogIds.Add(dialogId);
            }

            _dialogs.Clear();
            _pages.Clear();
            _context = newContext;
            _contextGeneration++;
            _activePageId = _primaryPageId;
            _pages[_primaryPageId] = new PageSlot(_primaryPageId, newPage, _contextGeneration);
            await oldContext.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            if (newPage is not null)
            {
                await newPage.CloseAsync().ConfigureAwait(false);
            }

            await newContext.CloseAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask WaitForUrlAsync(string url, WebTargetContext targetContext, int timeoutMilliseconds = 30000, CancellationToken cancellationToken = default)
    {
        await ApplyTargetAsync(targetContext, cancellationToken).ConfigureAwait(false);
        await ActivePage().WaitForURLAsync(url, new PageWaitForURLOptions { Timeout = BoundedTimeout(timeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask WaitForLoadStateAsync(WebNavigationWaitUntil state, WebTargetContext targetContext, int timeoutMilliseconds = 30000, CancellationToken cancellationToken = default)
    {
        await ApplyTargetAsync(targetContext, cancellationToken).ConfigureAwait(false);
        await ActivePage().WaitForLoadStateAsync(MapLoadState(state), new PageWaitForLoadStateOptions { Timeout = BoundedTimeout(timeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ILocator> ResolveSingleAsync(ResolvedLocatorPlan plan, int timeoutMilliseconds, int? elementIndex, string operation, CancellationToken cancellationToken, WebTargetContext? targetContext = null)
    {
        ILocator locator = await ResolveCollectionAsync(plan, timeoutMilliseconds, operation, cancellationToken, targetContext).ConfigureAwait(false);
        int count = await locator.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (count == 0)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.LocatorNotFound, "Locator did not match any elements.", operation));
        }

        if (elementIndex is not null)
        {
            if (elementIndex.Value < 0 || elementIndex.Value >= count)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.LocatorCardinalityMismatch, "Locator element index is outside the matched collection.", operation));
            }

            return locator.Nth(elementIndex.Value);
        }

        if (count != 1)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.LocatorCardinalityMismatch, "Locator matched multiple elements and no element index was supplied.", operation));
        }

        return locator;
    }

    private async ValueTask<ILocator> ResolveCollectionAsync(ResolvedLocatorPlan plan, int timeoutMilliseconds, string operation, CancellationToken cancellationToken, WebTargetContext? targetContext = null)
    {
        LocatorRoot root = await ResolveTargetRootAsync(targetContext, timeoutMilliseconds, operation, cancellationToken).ConfigureAwait(false);
        ILocator? scope = null;
        foreach (ResolvedLocatorScope item in plan.Scopes)
        {
            scope = await ResolveFirstAcceptedAsync(item.Strategies, item.Cardinality, root, scope, timeoutMilliseconds, operation, cancellationToken).ConfigureAwait(false);
        }

        return await ResolveFirstAcceptedAsync(plan.Strategies, plan.Cardinality, root, scope, timeoutMilliseconds, operation, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<LocatorRoot> ResolveTargetRootAsync(WebTargetContext? targetContext, int timeoutMilliseconds, string operation, CancellationToken cancellationToken)
    {
        WebTargetContext context = targetContext ?? new WebTargetContext();
        await ApplyTargetAsync(context, cancellationToken).ConfigureAwait(false);
        IFrame? frame = null;
        foreach (ResolvedLocatorPlan framePlan in context.Frames)
        {
            LocatorRoot root = new(ActivePage(), frame);
            ILocator frameLocator = await ResolveCollectionInRootAsync(framePlan, root, timeoutMilliseconds, "frame", cancellationToken).ConfigureAwait(false);
            int count = await frameLocator.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.FrameNotFound, "Frame locator did not match an iframe.", operation));
            }

            if (count != 1)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.FrameCardinalityMismatch, "Frame locator matched multiple iframes.", operation));
            }

            IElementHandle? element = await frameLocator.ElementHandleAsync(new LocatorElementHandleOptions { Timeout = BoundedTimeout(timeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false);
            frame = element is null ? null : await element.ContentFrameAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (frame is null)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.FrameNotFound, "Frame locator did not resolve to a content frame.", operation));
            }
        }

        return new LocatorRoot(ActivePage(), frame);
    }

    private async ValueTask<ILocator> ResolveCollectionInRootAsync(ResolvedLocatorPlan plan, LocatorRoot root, int timeoutMilliseconds, string operation, CancellationToken cancellationToken)
    {
        ILocator? scope = null;
        foreach (ResolvedLocatorScope item in plan.Scopes)
        {
            scope = await ResolveFirstAcceptedAsync(item.Strategies, item.Cardinality, root, scope, timeoutMilliseconds, operation, cancellationToken).ConfigureAwait(false);
        }

        return await ResolveFirstAcceptedAsync(plan.Strategies, plan.Cardinality, root, scope, timeoutMilliseconds, operation, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ILocator> ResolveFirstAcceptedAsync(IReadOnlyList<ResolvedLocatorStrategy> strategies, LocatorCardinality cardinality, LocatorRoot root, ILocator? scope, int timeoutMilliseconds, string operation, CancellationToken cancellationToken)
    {
        foreach (ResolvedLocatorStrategy strategy in strategies)
        {
            ILocator locator = CreateLocator(root, scope, strategy);
            try
            {
                await locator.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = BoundedTimeout(timeoutMilliseconds) }).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                if (cardinality is LocatorCardinality.ZeroOrOne or LocatorCardinality.Many)
                {
                    return locator;
                }

                continue;
            }

            int count = await locator.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (Accepts(cardinality, count))
            {
                return locator;
            }
        }

        throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.LocatorCardinalityMismatch, "No locator fallback satisfied cardinality.", operation));
    }

    private ILocator CreateLocator(LocatorRoot root, ILocator? scope, ResolvedLocatorStrategy strategy)
    {
        return strategy.Kind switch
        {
            "role" => RoleLocator(root, scope, strategy),
            "label" => Scope(root, scope).GetByLabel(strategy.Value ?? strategy.Name ?? string.Empty, new LocatorGetByLabelOptions { Exact = strategy.Match == LocatorTextMatchMode.Exact }),
            "placeholder" => Scope(root, scope).GetByPlaceholder(strategy.Value ?? string.Empty, new LocatorGetByPlaceholderOptions { Exact = strategy.Match == LocatorTextMatchMode.Exact }),
            "text" => Scope(root, scope).GetByText(strategy.Value ?? string.Empty, new LocatorGetByTextOptions { Exact = strategy.Match == LocatorTextMatchMode.Exact }),
            "test-id" => Scope(root, scope).Locator($"[{_testIdAttribute}=\"{CssEscape(strategy.Value ?? string.Empty)}\"]"),
            "title" => Scope(root, scope).GetByTitle(strategy.Value ?? string.Empty, new LocatorGetByTitleOptions { Exact = strategy.Match == LocatorTextMatchMode.Exact }),
            "alt-text" => Scope(root, scope).GetByAltText(strategy.Value ?? string.Empty, new LocatorGetByAltTextOptions { Exact = strategy.Match == LocatorTextMatchMode.Exact }),
            "css" => Scope(root, scope).Locator(strategy.Selector ?? string.Empty),
            "xpath" => Scope(root, scope).Locator("xpath=" + (strategy.Selector ?? string.Empty)),
            _ => throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.UnsupportedLocatorStrategy, "Unsupported locator strategy.", "locator")),
        };
    }

    private ILocator RoleLocator(LocatorRoot root, ILocator? scope, ResolvedLocatorStrategy strategy)
    {
        if (!Enum.TryParse(ToAriaRole(strategy.Role ?? string.Empty), ignoreCase: true, out AriaRole role))
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.UnsupportedLocatorStrategy, "Unsupported ARIA role.", "locator"));
        }

        return Scope(root, scope).GetByRole(role, new LocatorGetByRoleOptions { Name = strategy.Name, Exact = strategy.Match == LocatorTextMatchMode.Exact });
    }

    private static ILocator Scope(LocatorRoot root, ILocator? scope)
    {
        return scope ?? root.Frame?.Locator(":root") ?? root.Page.Locator(":root");
    }

    private IPage ActivePage()
    {
        return PageFor(new WebPageReference(_activePageId), failOnClosed: true);
    }

    private PageSlot SlotFor(WebPageReference page, bool failOnClosed)
    {
        if (_stalePageIds.Contains(page.PageId))
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StaleBrowsingContextReference, "Page reference belongs to a previous browser context.", "page"));
        }

        if (!_pages.TryGetValue(page.PageId, out PageSlot? slot))
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.UnknownPageReference, "Unknown page reference.", "page"));
        }

        if (slot.Generation != _contextGeneration)
        {
            _stalePageIds.Add(page.PageId);
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.StaleBrowsingContextReference, "Page reference belongs to a previous browser context.", "page"));
        }

        if (failOnClosed && slot.IsClosed)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.PageAlreadyClosed, "Page is already closed.", "page"));
        }

        return slot;
    }

    private IPage PageFor(WebPageReference page, bool failOnClosed)
    {
        return SlotFor(page, failOnClosed).Page;
    }

    private async ValueTask ApplyTargetAsync(WebTargetContext targetContext, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (targetContext.Page is not null)
        {
            _ = PageFor(targetContext.Page, failOnClosed: true);
            _activePageId = targetContext.Page.PageId;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string SafeUploadFilename(string filename)
    {
        return SafeFilename(filename, "upload.bin", rejectDeviceName: true);
    }

    private static string SafeDownloadFilename(string? filename)
    {
        return SafeFilename(filename, "download.bin", rejectDeviceName: false);
    }

    private static string SafeFilename(string? filename, string fallback, bool rejectDeviceName)
    {
        string name = string.IsNullOrWhiteSpace(filename) ? fallback : Path.GetFileName(filename);
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
        {
            name = fallback;
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        name = name.TrimEnd(' ', '.');
        if (name.Length == 0)
        {
            name = fallback;
        }

        string stem = Path.GetFileNameWithoutExtension(name);
        if (IsWindowsDeviceName(stem))
        {
            if (rejectDeviceName)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.UploadFailed, "Upload artifact filename is invalid.", "uploadFiles"));
            }

            name = "_" + name;
        }

        return name.Length > 128 ? name[..128] : name;
    }

    private static bool IsWindowsDeviceName(string value)
    {
        string upper = value.ToUpperInvariant();
        return upper is "CON" or "PRN" or "AUX" or "NUL" or "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9";
    }

    private static WebDialogKind MapDialogKind(string type)
    {
        return type switch
        {
            "alert" => WebDialogKind.Alert,
            "confirm" => WebDialogKind.Confirm,
            "prompt" => WebDialogKind.Prompt,
            "beforeunload" => WebDialogKind.BeforeUnload,
            _ => WebDialogKind.Alert,
        };
    }

    private static LoadState MapLoadState(WebNavigationWaitUntil state)
    {
        return state switch
        {
            WebNavigationWaitUntil.DomContentLoaded => LoadState.DOMContentLoaded,
            WebNavigationWaitUntil.NetworkIdle => LoadState.NetworkIdle,
            _ => LoadState.Load,
        };
    }

    private static SameSiteAttribute? MapSameSite(string value)
    {
        return value switch
        {
            "Strict" => SameSiteAttribute.Strict,
            "None" => SameSiteAttribute.None,
            _ => SameSiteAttribute.Lax,
        };
    }

    private async ValueTask RunActionAsync(Func<ValueTask> action, string operation, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.LocatorOperationTimeout, "Web operation timed out.", operation), exception);
        }
        catch (PlaywrightException exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebActionFailed, "Web operation failed.", operation), exception);
        }
    }

    private static bool Accepts(LocatorCardinality cardinality, int count)
    {
        return cardinality switch
        {
            LocatorCardinality.One => count == 1,
            LocatorCardinality.ZeroOrOne => count <= 1,
            LocatorCardinality.OneOrMore => count >= 1,
            LocatorCardinality.Many => count >= 0,
            _ => false,
        };
    }

    private static float BoundedTimeout(int value)
    {
        return value is > 0 and <= 300000 ? value : 30000;
    }

    private static WaitUntilState MapWait(WebNavigationWaitUntil wait)
    {
        return wait switch
        {
            WebNavigationWaitUntil.Commit => WaitUntilState.Commit,
            WebNavigationWaitUntil.DomContentLoaded => WaitUntilState.DOMContentLoaded,
            WebNavigationWaitUntil.NetworkIdle => WaitUntilState.NetworkIdle,
            _ => WaitUntilState.Load,
        };
    }

    private static WaitForSelectorState MapWaitState(WebWaitState state)
    {
        return state switch
        {
            WebWaitState.Attached => WaitForSelectorState.Attached,
            WebWaitState.Hidden => WaitForSelectorState.Hidden,
            WebWaitState.Detached => WaitForSelectorState.Detached,
            _ => WaitForSelectorState.Visible,
        };
    }

    private static ScreenshotType MapScreenshotType(WebScreenshotFormat format)
    {
        return format == WebScreenshotFormat.Jpeg ? ScreenshotType.Jpeg : ScreenshotType.Png;
    }

    private static MouseButton MapButton(string button)
    {
        return button switch
        {
            "middle" => MouseButton.Middle,
            "right" => MouseButton.Right,
            _ => MouseButton.Left,
        };
    }

    private static string ToAriaRole(string role)
    {
        return role.Replace("-", string.Empty, StringComparison.Ordinal);
    }

    private static string CssEscape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static BrowserNewContextOptions CloneContextOptions(BrowserNewContextOptions source)
    {
        return new BrowserNewContextOptions
        {
            Locale = source.Locale,
            UserAgent = source.UserAgent,
            ViewportSize = source.ViewportSize,
            ServiceWorkers = source.ServiceWorkers,
        };
    }

    private sealed record PageSlot(string Id, IPage Page, int Generation, bool IsClosed = false)
    {
        public string Order => Id == _primaryPageId ? "page-0000000001" : Id;
    }

    private sealed record DialogSlot(IDialog Dialog, int Generation);

    private sealed record LocatorRoot(IPage Page, IFrame? Frame);
}
