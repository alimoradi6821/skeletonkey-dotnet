using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Playwright;

/// <summary>Captures bounded provider-neutral page evidence after a web node failure.</summary>
public sealed class PlaywrightFailureDiagnosticContributor : INodeFailureDiagnosticContributor
{
    /// <inheritdoc />
    public string Id => "web.playwright";

    /// <inheritdoc />
    public bool AppliesTo(WorkflowNodeDefinitionKey definition)
    {
        return definition.Type.StartsWith("web.", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public async ValueTask<NodeFailureDiagnosticContribution?> CaptureAsync(
        NodeExecutionIdentity identity,
        WorkflowError error,
        INodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Resources.TryGetBinding("page", out NodeResourceBinding? binding) || binding is null)
        {
            return null;
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        await using INodeResourceLease lease = await context.Resources.AcquireAsync("page", timeout.Token).ConfigureAwait(false);
        IWebPageAdapter adapter = lease.Resource.GetRequiredAdapter<IWebPageAdapter>();
        WebPageCollectionSnapshot snapshot = await adapter.ListPagesAsync(timeout.Token).ConfigureAwait(false);
        WebPageInformation? active = snapshot.Pages.FirstOrDefault(static page => page.IsActive && !page.IsClosed);
        JsonObject data = new()
        {
            ["pageCount"] = snapshot.Pages.Count(static page => !page.IsClosed),
        };
        if (active is not null)
        {
            data["url"] = SafeUrl(active.Url);
            data["title"] = active.Title;
        }

        return new NodeFailureDiagnosticContribution(Id, data);
    }

    private static string SafeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed))
        {
            return string.Empty;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            return parsed.Scheme + ":";
        }

        UriBuilder builder = new(parsed)
        {
            Query = string.Empty,
            Fragment = string.Empty,
            UserName = string.Empty,
            Password = string.Empty,
        };
        string safe = builder.Uri.GetLeftPart(UriPartial.Path);
        return safe.Length <= 2048 ? safe : safe[..2048];
    }
}
