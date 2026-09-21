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
    private readonly bool _ownsBrowser;
    private readonly ManagedCdpBrowserHost? _ownedManagedCdpBrowserHost;
    private bool _disposed;

    private PlaywrightPageResource(
        string resourceName,
        WorkflowResourceAccessMode access,
        IPlaywright playwright,
        IBrowser? browser,
        IReadOnlyList<string> capabilities,
        PlaywrightPageAdapter adapter,
        IWorkflowArtifactStore? artifactStore,
        bool ownsBrowser,
        ManagedCdpBrowserHost? ownedManagedCdpBrowserHost)
    {
        ResourceName = resourceName;
        Access = access;
        _playwright = playwright;
        _browser = browser;
        _capabilities = new ReadOnlyCollection<string>([.. capabilities]);
        _adapter = adapter;
        _artifactStore = artifactStore;
        _ownsBrowser = ownsBrowser;
        _ownedManagedCdpBrowserHost = ownedManagedCdpBrowserHost;
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

        if (recovery is not null && constraints.Connection is "cdp" or "cdp-managed")
        {
            throw new ArgumentException("CDP-attached browser resources do not support checkpoint reconstruction.", nameof(checkpointState));
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
        bool ownsBrowser = false;
        bool ownsContext = true;
        bool allowContextReplacement = true;
        bool supportsCheckpointRecovery = true;
        ManagedCdpBrowserHost? ownedManagedCdpBrowserHost = null;
        if (constraints.Connection is "cdp" or "cdp-managed")
        {
            string endpoint;
            if (constraints.Connection == "cdp")
            {
                ValidateCdpEndpointPolicy(constraints.CdpEndpoint!, options);
                endpoint = constraints.CdpEndpoint!;
            }
            else
            {
                ManagedCdpBrowserHost managedCdpBrowserHost = options.ManagedCdpBrowserHost ?? new ManagedCdpBrowserHost();
                if (options.ManagedCdpBrowserHost is null)
                {
                    ownedManagedCdpBrowserHost = managedCdpBrowserHost;
                }

                try
                {
                    endpoint = await managedCdpBrowserHost.GetOrStartEndpointAsync(
                        constraints,
                        options.CdpConnectTimeoutMilliseconds,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException and not WebAutomationException)
                {
                    if (ownedManagedCdpBrowserHost is not null)
                    {
                        await ownedManagedCdpBrowserHost.DisposeAsync().ConfigureAwait(false);
                    }

                    throw new WebAutomationException(
                        new WebOperationError(WebAutomationErrorCodes.BrowserConnectionFailed, "Managed CDP browser startup failed.", "connect"),
                        exception);
                }
            }

            try
            {
                browser = await playwright.Chromium.ConnectOverCDPAsync(
                    endpoint,
                    new BrowserTypeConnectOverCDPOptions { Timeout = options.CdpConnectTimeoutMilliseconds }).ConfigureAwait(false);
            }
            catch (PlaywrightException exception)
            {
                if (ownedManagedCdpBrowserHost is not null)
                {
                    await ownedManagedCdpBrowserHost.DisposeAsync().ConfigureAwait(false);
                }

                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.BrowserConnectionFailed, "CDP browser connection failed.", "connect"), exception);
            }

            context = browser.Contexts.FirstOrDefault()
                ?? throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.BrowserConnectionFailed, "CDP browser did not expose a default context.", "connect"));
            ownsContext = false;
            allowContextReplacement = false;
            supportsCheckpointRecovery = false;
        }
        else if (constraints.Persistent)
        {
            string userDataDirectory = Environment.ExpandEnvironmentVariables(constraints.UserDataDirectory!);
            context = await browserType.LaunchPersistentContextAsync(
                userDataDirectory,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Channel = constraints.Channel,
                    Headless = constraints.Headless,
                    Locale = constraints.Locale,
                    UserAgent = constraints.UserAgent,
                    ViewportSize = contextOptions.ViewportSize,
                    ServiceWorkers = constraints.NetworkPolicy is null ? null : ServiceWorkerPolicy.Block,
                }).ConfigureAwait(false);
        }
        else
        {
            browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions { Channel = constraints.Channel, Headless = constraints.Headless }).ConfigureAwait(false);
            ownsBrowser = true;
            context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
        }

        context.SetDefaultTimeout(constraints.DefaultTimeoutMilliseconds);
        PlaywrightNetworkInterceptor? networkInterceptor = constraints.NetworkPolicy is null ? null : new PlaywrightNetworkInterceptor(constraints.NetworkPolicy);
        if (networkInterceptor is not null)
        {
            await networkInterceptor.AttachAsync(context, cancellationToken).ConfigureAwait(false);
        }

        IPage page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync().ConfigureAwait(false);
        PlaywrightPageAdapter adapter = new(
            browser,
            context,
            contextOptions,
            page,
            options.NavigationPolicy,
            options.TestIdAttribute,
            constraints.DefaultTimeoutMilliseconds,
            networkInterceptor,
            ownsContext,
            allowContextReplacement,
            supportsCheckpointRecovery);
        if (recovery is not null)
        {
            await adapter.RestoreCheckpointStateAsync(recovery, cancellationToken).ConfigureAwait(false);
        }

        return new PlaywrightPageResource(
            request.ResourceName,
            request.Definition.Access,
            playwright,
            browser,
            capabilities,
            adapter,
            options.ArtifactStore,
            ownsBrowser,
            ownedManagedCdpBrowserHost);
    }

    private static void ValidateCdpEndpointPolicy(string endpoint, PlaywrightPageProviderOptions options)
    {
        Uri uri = new(endpoint, UriKind.Absolute);
        if (!options.AllowRemoteCdpEndpoints && !uri.IsLoopback)
        {
            throw new ArgumentException("Remote CDP endpoints are disabled by the host.");
        }
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
        if (_ownsBrowser && _browser is not null)
        {
            await TryDisposeAsync(async () => await _browser.CloseAsync().ConfigureAwait(false), errors).ConfigureAwait(false);
        }

        _playwright.Dispose();
        if (_ownedManagedCdpBrowserHost is not null)
        {
            await TryDisposeAsync(async () => await _ownedManagedCdpBrowserHost.DisposeAsync().ConfigureAwait(false), errors).ConfigureAwait(false);
        }
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