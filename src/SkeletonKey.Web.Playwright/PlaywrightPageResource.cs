using System.Collections.ObjectModel;
using Microsoft.Playwright;
using SkeletonKey.Artifacts;
using SkeletonKey.Execution;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.Playwright;

/// <summary>
/// Owns a Playwright page together with its internal browser, context, and Playwright lifetime.
/// </summary>
public sealed class PlaywrightPageResource : IWorkflowRuntimeResourceInstance, IWorkflowRuntimeResourceCheckpointParticipant
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser? _browser;
    private readonly PlaywrightPageAdapter _adapter;
    private readonly IWorkflowArtifactStore? _artifactStore;
    private readonly IReadOnlyList<string> _capabilities;
    private bool _disposed;

    private PlaywrightPageResource(string resourceName, WorkflowResourceAccessMode access, IPlaywright playwright, IBrowser? browser, IReadOnlyList<string> capabilities, PlaywrightPageAdapter adapter, IWorkflowArtifactStore? artifactStore)
    {
        ResourceName = resourceName;
        Access = access;
        _playwright = playwright;
        _browser = browser;
        _capabilities = new ReadOnlyCollection<string>([.. capabilities]);
        _adapter = adapter;
        _artifactStore = artifactStore;
    }

    /// <summary>
    /// Creates a Playwright page resource.
    /// </summary>
    public static async ValueTask<PlaywrightPageResource> CreateAsync(
        WorkflowRuntimeResourceRequest request,
        IPlaywright playwright,
        PlaywrightPageConstraints constraints,
        PlaywrightPageProviderOptions options,
        IReadOnlyList<string> capabilities,
        CancellationToken cancellationToken,
        WorkflowRuntimeResourceCheckpointState? checkpointState = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PlaywrightPageCheckpointState? recovery = checkpointState is null ? null : PlaywrightPageCheckpointState.Parse(checkpointState);
        if (recovery is not null && constraints.Persistent)
        {
            throw new ArgumentException("Persistent Playwright contexts do not support checkpoint reconstruction.", nameof(checkpointState));
        }

        IBrowserType browserType = constraints.Engine switch
        {
            "firefox" => playwright.Firefox,
            "webkit" => playwright.Webkit,
            _ => playwright.Chromium,
        };

        BrowserNewContextOptions contextOptions = new()
        {
            Locale = constraints.Locale,
            UserAgent = constraints.UserAgent,
            ServiceWorkers = constraints.NetworkPolicy is null ? null : ServiceWorkerPolicy.Block,
            StorageState = recovery?.StorageState,
        };
        if (constraints.ViewportWidth is not null && constraints.ViewportHeight is not null)
        {
            contextOptions.ViewportSize = new ViewportSize { Width = constraints.ViewportWidth.Value, Height = constraints.ViewportHeight.Value };
        }

        IBrowser? browser = null;
        IBrowserContext context;
        if (constraints.Persistent)
        {
            context = await browserType.LaunchPersistentContextAsync(
                constraints.UserDataDirectory!,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = constraints.Headless,
                    Locale = constraints.Locale,
                    UserAgent = constraints.UserAgent,
                    ViewportSize = contextOptions.ViewportSize,
                    ServiceWorkers = constraints.NetworkPolicy is null ? null : ServiceWorkerPolicy.Block,
                }).ConfigureAwait(false);
        }
        else
        {
            browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions { Headless = constraints.Headless }).ConfigureAwait(false);
            context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
        }

        context.SetDefaultTimeout(constraints.DefaultTimeoutMilliseconds);
        PlaywrightNetworkInterceptor? networkInterceptor = constraints.NetworkPolicy is null ? null : new PlaywrightNetworkInterceptor(constraints.NetworkPolicy);
        if (networkInterceptor is not null)
        {
            await networkInterceptor.AttachAsync(context, cancellationToken).ConfigureAwait(false);
        }

        IPage page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync().ConfigureAwait(false);
        PlaywrightPageAdapter adapter = new(browser, context, contextOptions, page, options.NavigationPolicy, options.TestIdAttribute, constraints.DefaultTimeoutMilliseconds, networkInterceptor);
        if (recovery is not null)
        {
            await adapter.RestoreCheckpointStateAsync(recovery, cancellationToken).ConfigureAwait(false);
        }

        return new PlaywrightPageResource(request.ResourceName, request.Definition.Access, playwright, browser, capabilities, adapter, options.ArtifactStore);
    }

    /// <inheritdoc />
    public string ResourceName { get; }

    /// <inheritdoc />
    public string Kind => StandardWorkflowResourceKinds.WebPage;

    /// <inheritdoc />
    public string InstanceId { get; } = "playwright:web.page";

    /// <inheritdoc />
    public IReadOnlyList<string> Capabilities => new ReadOnlyCollection<string>([.. _capabilities]);

    /// <inheritdoc />
    public WorkflowResourceAccessMode Access { get; }

    /// <inheritdoc />
    public INodeResourceHandle CreateHandle()
    {
        return new PlaywrightPageResourceHandle(ResourceName, Kind, InstanceId, Capabilities, _adapter, _artifactStore);
    }

    /// <inheritdoc />
    public ValueTask<WorkflowRuntimeResourceCheckpointState?> CaptureCheckpointStateAsync(CancellationToken cancellationToken = default)
    {
        return _adapter.CaptureCheckpointStateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        List<Exception> errors = [];
        await TryDisposeAsync(async () => await _adapter.DisposeAsync().ConfigureAwait(false), errors).ConfigureAwait(false);
        if (_browser is not null)
        {
            await TryDisposeAsync(async () => await _browser.CloseAsync().ConfigureAwait(false), errors).ConfigureAwait(false);
        }

        _playwright.Dispose();
        if (errors.Count == 1)
        {
            throw errors[0];
        }

        if (errors.Count > 1)
        {
            throw new AggregateException(errors);
        }
    }

    private static async ValueTask TryDisposeAsync(Func<ValueTask> action, List<Exception> errors)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }

    private sealed class PlaywrightPageResourceHandle(string resourceName, string kind, string instanceId, IReadOnlyList<string> capabilities, IWebPageAdapter adapter, IWorkflowArtifactStore? artifactStore) : INodeResourceHandle
    {
        public string ResourceName { get; } = resourceName;

        public string Kind { get; } = kind;

        public string InstanceId { get; } = instanceId;

        public IReadOnlyList<string> Capabilities { get; } = new ReadOnlyCollection<string>([.. capabilities]);

        public bool TryGetAdapter<TAdapter>(out TAdapter? typedAdapter)
            where TAdapter : class
        {
            typedAdapter = adapter as TAdapter ?? artifactStore as TAdapter;
            return typedAdapter is not null;
        }

        public TAdapter GetRequiredAdapter<TAdapter>()
            where TAdapter : class
        {
            return TryGetAdapter(out TAdapter? typedAdapter) && typedAdapter is not null
                ? typedAdapter
                : throw new InvalidOperationException("The requested resource adapter is not available.");
        }
    }
}
