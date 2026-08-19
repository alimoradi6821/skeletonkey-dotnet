using Microsoft.Playwright;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Playwright;

internal sealed class PlaywrightNetworkInterceptor(WebNetworkInterceptionPolicy policy)
{
    private int _interceptionCount;

    public async ValueTask AttachAsync(IBrowserContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await context.RouteAsync("**/*", HandleAsync).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleAsync(IRoute route)
    {
        try
        {
            if (Interlocked.Increment(ref _interceptionCount) > policy.MaximumInterceptions)
            {
                await route.AbortAsync().ConfigureAwait(false);
                return;
            }

            WebNetworkRequest request = new(route.Request.Method, route.Request.Url, route.Request.ResourceType);
            WebNetworkInterceptionDecision decision = policy.Evaluate(request);
            switch (decision.Action)
            {
                case WebNetworkInterceptionAction.Block:
                    await route.AbortAsync().ConfigureAwait(false);
                    break;
                case WebNetworkInterceptionAction.Modify:
                    await ModifyAsync(route, decision.Rule!).ConfigureAwait(false);
                    break;
                case WebNetworkInterceptionAction.Fulfill:
                    await FulfillAsync(route, decision.Rule!).ConfigureAwait(false);
                    break;
                default:
                    await route.ContinueAsync().ConfigureAwait(false);
                    break;
            }
        }
        catch (PlaywrightException)
        {
            await TryAbortAsync(route).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await TryAbortAsync(route).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            await TryAbortAsync(route).ConfigureAwait(false);
        }
    }

    private static async Task ModifyAsync(IRoute route, WebNetworkInterceptionRule rule)
    {
        Dictionary<string, string> headers = new(route.Request.Headers, StringComparer.OrdinalIgnoreCase);
        foreach (string header in rule.RemovedRequestHeaders)
        {
            headers.Remove(header);
        }

        foreach (KeyValuePair<string, string> header in rule.RequestHeaders)
        {
            headers[header.Key] = header.Value;
        }

        await route.ContinueAsync(new RouteContinueOptions { Headers = headers }).ConfigureAwait(false);
    }

    private static async Task FulfillAsync(IRoute route, WebNetworkInterceptionRule rule)
    {
        var headers = rule.ResponseHeaders.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        await route.FulfillAsync(new RouteFulfillOptions
        {
            Status = rule.ResponseStatus,
            ContentType = rule.ResponseContentType,
            Body = rule.ResponseBody,
            Headers = headers,
        }).ConfigureAwait(false);
    }

    private static async Task TryAbortAsync(IRoute route)
    {
        try
        {
            await route.AbortAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
        }
    }
}
