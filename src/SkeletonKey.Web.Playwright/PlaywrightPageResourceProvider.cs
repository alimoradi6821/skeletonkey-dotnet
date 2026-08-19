using Microsoft.Playwright;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.Playwright;

/// <summary>
/// Creates Playwright-backed runtime resources for the provider-neutral <c>web.page</c> kind.
/// </summary>
public sealed class PlaywrightPageResourceProvider : IWorkflowRuntimeResourceRecoveryProvider
{
    private readonly PlaywrightPageProviderOptions _options;

    /// <summary>
    /// Initializes a Playwright page resource provider.
    /// </summary>
    public PlaywrightPageResourceProvider(PlaywrightPageProviderOptions? options = null)
    {
        _options = options ?? new PlaywrightPageProviderOptions();
    }

    /// <inheritdoc />
    public string Kind => StandardWorkflowResourceKinds.WebPage;

    /// <inheritdoc />
    public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(
    [
        StandardWorkflowResourceCapabilities.WebNavigation,
        StandardWorkflowResourceCapabilities.WebActions,
        StandardWorkflowResourceCapabilities.WebLocators,
        StandardWorkflowResourceCapabilities.WebText,
        StandardWorkflowResourceCapabilities.WebAttributes,
        StandardWorkflowResourceCapabilities.WebForms,
        StandardWorkflowResourceCapabilities.WebScreenshot,
        StandardWorkflowResourceCapabilities.WebNetworkInterception,
    ]);

    /// <inheritdoc />
    public async ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(WorkflowRuntimeResourceRequest request, CancellationToken cancellationToken = default)
    {
        return await CreateCoreAsync(request, checkpointState: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IWorkflowRuntimeResourceInstance> RestoreAsync(
        WorkflowRuntimeResourceRequest request,
        WorkflowRuntimeResourceCheckpointState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        return await CreateCoreAsync(request, state, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IWorkflowRuntimeResourceInstance> CreateCoreAsync(
        WorkflowRuntimeResourceRequest request,
        WorkflowRuntimeResourceCheckpointState? checkpointState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.Definition.Kind, Kind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Playwright page provider only supports web.page resources.");
        }

        var constraints = PlaywrightPageConstraints.Parse(request.Definition.Constraints);
        IPlaywright? playwright = null;
        try
        {
            playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
            PlaywrightPageResource resource = await PlaywrightPageResource.CreateAsync(request, playwright, constraints, _options, Capabilities, cancellationToken, checkpointState).ConfigureAwait(false);
            return resource;
        }
        catch (ArgumentException)
        {
            playwright?.Dispose();
            throw;
        }
        catch (OperationCanceledException)
        {
            playwright?.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            playwright?.Dispose();
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.BrowserLaunchFailed, "Browser launch failed.", "create"), exception);
        }
    }
}
