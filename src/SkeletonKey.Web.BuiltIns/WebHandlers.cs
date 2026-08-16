using System.Text.Json.Nodes;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.BuiltIns;

/// <summary>Executes <c>web.navigate</c>.</summary>
public sealed class WebNavigateHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebNavigateHandler() : base("web.navigate") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject parameters = request.Parameters;
        string url = RequiredString(parameters, "url");
        string finalUrl = await adapter.NavigateAsync(new WebNavigationRequest(url, ParseWaitUntil(OptionalString(parameters, "waitUntil", "load")), OptionalInt(parameters, "timeoutMilliseconds", 30000)), cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["url"] = StringValues([finalUrl]) });
    }

    private static WebNavigationWaitUntil ParseWaitUntil(string value)
    {
        return value switch
        {
            "commit" => WebNavigationWaitUntil.Commit,
            "domcontentloaded" or "dom-content-loaded" => WebNavigationWaitUntil.DomContentLoaded,
            "networkidle" or "network-idle" => WebNavigationWaitUntil.NetworkIdle,
            _ => WebNavigationWaitUntil.Load,
        };
    }
}

/// <summary>Executes <c>web.click</c>.</summary>
public sealed class WebClickHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebClickHandler() : base("web.click") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.ClickAsync(RequiredLocator(context, "target"), new WebClickRequest(OptionalString(p, "button", "left"), OptionalInt(p, "clickCount", 1), OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.fill</c>.</summary>
public sealed class WebFillHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebFillHandler() : base("web.fill") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.FillAsync(RequiredLocator(context, "target"), new WebFillRequest(RequiredString(p, "value"), OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.press</c>.</summary>
public sealed class WebPressHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebPressHandler() : base("web.press") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.PressAsync(RequiredLocator(context, "target"), new WebPressRequest(RequiredString(p, "key"), OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.selectOption</c>.</summary>
public sealed class WebSelectOptionHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebSelectOptionHandler() : base("web.selectOption") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        IReadOnlyList<string> values = p["values"] is JsonArray array
            ? array.Select(static item => item!.GetValue<string>()).ToArray()
            : [RequiredString(p, "value")];
        await adapter.SelectOptionAsync(RequiredLocator(context, "target"), new WebSelectOptionRequest(values, OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.setChecked</c>.</summary>
public sealed class WebSetCheckedHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebSetCheckedHandler() : base("web.setChecked") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.SetCheckedAsync(RequiredLocator(context, "target"), new WebSetCheckedRequest(RequiredBool(p, "checked"), OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.wait</c>.</summary>
public sealed class WebWaitHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebWaitHandler() : base("web.wait") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        WebWaitState state = OptionalString(p, "state", "visible") switch
        {
            "attached" => WebWaitState.Attached,
            "hidden" => WebWaitState.Hidden,
            "detached" => WebWaitState.Detached,
            _ => WebWaitState.Visible,
        };
        await adapter.WaitAsync(RequiredLocator(context, "target"), new WebWaitRequest(state, OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.getText</c>.</summary>
public sealed class WebGetTextHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebGetTextHandler() : base("web.getText") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        IReadOnlyList<string?> values = await adapter.GetTextAsync(RequiredLocator(context, "target"), OptionalInt(request.Parameters, "timeoutMilliseconds", 30000), TargetContext(context, request.Parameters), cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["result"] = StringValues(values) });
    }
}

/// <summary>Executes <c>web.getAttribute</c>.</summary>
public sealed class WebGetAttributeHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebGetAttributeHandler() : base("web.getAttribute") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        IReadOnlyList<string?> values = await adapter.GetAttributeAsync(RequiredLocator(context, "target"), RequiredString(p, "name"), OptionalInt(p, "timeoutMilliseconds", 30000), TargetContext(context, p), cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["result"] = StringValues(values) });
    }
}

/// <summary>Executes <c>web.getCount</c>.</summary>
public sealed class WebGetCountHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebGetCountHandler() : base("web.getCount") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        int count = await adapter.GetCountAsync(RequiredLocator(context, "target"), OptionalInt(request.Parameters, "timeoutMilliseconds", 30000), TargetContext(context, request.Parameters), cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["count"] = new([JsonValue.Create(count)]) });
    }
}

/// <summary>Executes <c>web.screenshot</c>.</summary>
public sealed class WebScreenshotHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebScreenshotHandler() : base("web.screenshot") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        WebScreenshotFormat format = OptionalString(p, "format", "png") == "jpeg" ? WebScreenshotFormat.Jpeg : WebScreenshotFormat.Png;
        ResolvedLocatorPlan? locator = context.Locators.TryGet("target", out ResolvedLocatorPlan? found) ? found : null;
        WebScreenshotResult result = await adapter.ScreenshotAsync(locator, new WebScreenshotRequest(format, OptionalInt(p, "timeoutMilliseconds", 30000), OptionalInt(p, "maximumBytes", 4 * 1024 * 1024), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)), cancellationToken).ConfigureAwait(false);
        JsonObject image = new() { ["mediaType"] = result.MediaType, ["base64"] = result.Base64 };
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["image"] = new([image]) });
    }
}
