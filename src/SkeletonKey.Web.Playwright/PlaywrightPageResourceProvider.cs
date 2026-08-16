using Microsoft.Playwright;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.Playwright;

/// <summary>
/// Creates Playwright-backed runtime resources for the provider-neutral <c>web.page</c> kind.
/// </summary>
public sealed class PlaywrightPageResourceProvider : IWorkflowRuntimeResourceProvider
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
    ]);

    /// <inheritdoc />
    public async ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(WorkflowRuntimeResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.Definition.Kind, Kind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Playwright page provider only supports web.page resources.");
        }

        var constraints = PlaywrightPageConstraints.Parse(request.Definition.Constraints);
        try
        {
            IPlaywright playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
            PlaywrightPageResource resource = await PlaywrightPageResource.CreateAsync(request, playwright, constraints, _options, Capabilities, cancellationToken).ConfigureAwait(false);
            return resource;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.BrowserLaunchFailed, "Browser launch failed.", "create"), exception);
        }
    }
}
