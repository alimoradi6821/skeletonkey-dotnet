using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Artifacts;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Web.Abstractions;
using static SkeletonKey.Web.BuiltIns.WebAdvancedHandlerJson;

namespace SkeletonKey.Web.BuiltIns;

/// <summary>Executes <c>web.openPage</c>.</summary>
public sealed class WebOpenPageHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebOpenPageHandler() : base("web.openPage") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        (WebPageReference page, string url) = await adapter.OpenPageAsync(
            new WebNavigationRequest(RequiredString(p, "url"), ParseWaitUntil(OptionalString(p, "waitUntil", "load")), OptionalInt(p, "timeoutMilliseconds", 30000)),
            OptionalBool(p, "activate", true),
            cancellationToken).ConfigureAwait(false);

        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["page"] = JsonValues([PageJson(page)]),
            ["url"] = StringValues([url]),
        });
    }
}

/// <summary>Executes <c>web.listPages</c>.</summary>
public sealed class WebListPagesHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebListPagesHandler() : base("web.listPages") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        WebPageCollectionSnapshot snapshot = await adapter.ListPagesAsync(cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["pages"] = JsonValues(snapshot.Pages.Select(PageInfoJson)),
        });
    }
}

/// <summary>Executes <c>web.activatePage</c>.</summary>
public sealed class WebActivatePageHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebActivatePageHandler() : base("web.activatePage") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        await adapter.ActivatePageAsync(RequiredPageReference(request.Parameters), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.closePage</c>.</summary>
public sealed class WebClosePageHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebClosePageHandler() : base("web.closePage") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        await adapter.ClosePageAsync(RequiredPageReference(request.Parameters), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.clickAndWaitForPopup</c>.</summary>
public sealed class WebClickAndWaitForPopupHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebClickAndWaitForPopupHandler() : base("web.clickAndWaitForPopup") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        WebPopupRequest popupRequest = new(
            TargetContext(context, p),
            OptionalString(p, "button", "left"),
            OptionalInt(p, "clickCount", 1),
            OptionalInt(p, "timeoutMilliseconds", 30000),
            OptionalBool(p, "activatePopup", true),
            OptionalNullableInt(p, "elementIndex"));
        (WebPageReference page, string url) = await adapter.ClickAndWaitForPopupAsync(RequiredLocator(context, "target"), popupRequest, cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["page"] = JsonValues([PageJson(page)]),
            ["url"] = StringValues([url]),
        });
    }
}

/// <summary>Executes <c>web.uploadFiles</c>.</summary>
public sealed class WebUploadFilesHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebUploadFilesHandler() : base("web.uploadFiles") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, INodeResourceHandle resource, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        IReadOnlyList<WorkflowArtifactReference> artifacts = ArtifactReferences(p);
        await adapter.UploadFilesAsync(
            RequiredLocator(context, "target"),
            new WebUploadFilesRequest(
                TargetContext(context, p),
                artifacts,
                OptionalInt(p, "timeoutMilliseconds", 30000),
                OptionalNullableInt(p, "elementIndex"),
                OptionalInt(p, "maximumFiles", 16),
                OptionalNullableLong(p, "maximumAggregateBytes") ?? 64 * 1024 * 1024),
            RequiredArtifactStore(resource),
            cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["artifacts"] = JsonValues(artifacts.Select(ArtifactJson)),
        });
    }
}

/// <summary>Executes <c>web.clickAndWaitForDownload</c>.</summary>
public sealed class WebClickAndWaitForDownloadHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebClickAndWaitForDownloadHandler() : base("web.clickAndWaitForDownload") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, INodeResourceHandle resource, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        WorkflowArtifactReference artifact = await adapter.ClickAndWaitForDownloadAsync(
            RequiredLocator(context, "target"),
            new WebDownloadRequest(TargetContext(context, p), OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableLong(p, "maximumBytes") ?? 64 * 1024 * 1024, ParseSensitivity(OptionalString(p, "sensitivity", "internal")), OptionalNullableInt(p, "elementIndex")),
            RequiredArtifactStore(resource),
            cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["artifact"] = JsonValues([ArtifactJson(artifact)]) });
    }
}

/// <summary>Executes <c>web.clickAndWaitForDialog</c>.</summary>
public sealed class WebClickAndWaitForDialogHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebClickAndWaitForDialogHandler() : base("web.clickAndWaitForDialog") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        WebDialogInformation dialog = await adapter.ClickAndWaitForDialogAsync(
            RequiredLocator(context, "target"),
            new WebDialogWaitRequest(TargetContext(context, p), OptionalString(p, "button", "left"), OptionalInt(p, "clickCount", 1), OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex")),
            cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["dialog"] = JsonValues([DialogJson(dialog)]) });
    }
}

/// <summary>Executes <c>web.respondDialog</c>.</summary>
public sealed class WebRespondDialogHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebRespondDialogHandler() : base("web.respondDialog") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.RespondDialogAsync(RequiredDialogReference(p), OptionalString(p, "action", "accept"), OptionalNullableString(p, "promptText"), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.getCookies</c>.</summary>
public sealed class WebGetCookiesHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebGetCookiesHandler() : base("web.getCookies") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        IReadOnlyList<WebCookie> cookies = await adapter.GetCookiesAsync(OptionalStringArray(request.Parameters, "urls"), cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["cookies"] = JsonValues(cookies.Select(CookieJson)) });
    }
}

/// <summary>Executes <c>web.setCookies</c>.</summary>
public sealed class WebSetCookiesHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebSetCookiesHandler() : base("web.setCookies") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        await adapter.SetCookiesAsync(CookieReferences(request.Parameters), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.clearCookies</c>.</summary>
public sealed class WebClearCookiesHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebClearCookiesHandler() : base("web.clearCookies") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.ClearCookiesAsync(OptionalNullableString(p, "name"), OptionalNullableString(p, "domain"), OptionalNullableString(p, "path"), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.exportStorageState</c>.</summary>
public sealed class WebExportStorageStateHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebExportStorageStateHandler() : base("web.exportStorageState") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, INodeResourceHandle resource, CancellationToken cancellationToken)
    {
        WorkflowArtifactReference artifact = await adapter.ExportStorageStateAsync(RequiredArtifactStore(resource), new WebStorageStateRequest(OptionalNullableLong(request.Parameters, "maximumBytes") ?? 4 * 1024 * 1024), cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["artifact"] = JsonValues([ArtifactJson(artifact)]) });
    }
}

/// <summary>Executes <c>web.importStorageState</c>.</summary>
public sealed class WebImportStorageStateHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebImportStorageStateHandler() : base("web.importStorageState") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, INodeResourceHandle resource, CancellationToken cancellationToken)
    {
        await adapter.ImportStorageStateAsync(RequiredArtifactStore(resource), ArtifactReference(RequiredObject(request.Parameters, "artifact")), cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["context"] = JsonValues([new JsonObject { ["imported"] = true }]) });
    }
}

/// <summary>Executes <c>web.waitForUrl</c>.</summary>
public sealed class WebWaitForUrlHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebWaitForUrlHandler() : base("web.waitForUrl") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.WaitForUrlAsync(RequiredString(p, "url"), TargetContext(context, p), OptionalInt(p, "timeoutMilliseconds", 30000), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.waitForLoadState</c>.</summary>
public sealed class WebWaitForLoadStateHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebWaitForLoadStateHandler() : base("web.waitForLoadState") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.WaitForLoadStateAsync(ParseWaitUntil(OptionalString(p, "state", "load")), TargetContext(context, p), OptionalInt(p, "timeoutMilliseconds", 30000), cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

file static class WebAdvancedHandlerJson
{
    public static WebNavigationWaitUntil ParseWaitUntil(string value)
    {
        return value switch
        {
            "commit" => WebNavigationWaitUntil.Commit,
            "domcontentloaded" or "dom-content-loaded" => WebNavigationWaitUntil.DomContentLoaded,
            "networkidle" or "network-idle" => WebNavigationWaitUntil.NetworkIdle,
            _ => WebNavigationWaitUntil.Load,
        };
    }

    public static WebTargetContext TargetContext(JsonObject parameters)
    {
        return new WebTargetContext(OptionalPageReference(parameters));
    }

    public static WebPageReference RequiredPageReference(JsonObject parameters)
    {
        WebPageReference? page = OptionalPageReference(parameters);
        return page ?? throw ParameterError("page", "Parameter 'page' must be a page reference.");
    }

    public static WebPageReference? OptionalPageReference(JsonObject parameters)
    {
        JsonNode? node = parameters["page"];
        if (node is null)
        {
            return null;
        }

        string? id = StringValue(node);
        if (id is null && node is JsonObject obj)
        {
            id = StringProperty(obj, "id") ?? StringProperty(obj, "pageId");
        }

        return string.IsNullOrWhiteSpace(id) ? throw ParameterError("page", "Parameter 'page' must be a page reference.") : new WebPageReference(id);
    }

    public static JsonObject PageJson(WebPageReference page)
    {
        return new JsonObject { ["id"] = page.PageId };
    }

    public static JsonObject PageInfoJson(WebPageInformation page)
    {
        return new JsonObject
        {
            ["id"] = page.Reference.PageId,
            ["url"] = page.Url,
            ["title"] = page.Title,
            ["isActive"] = page.IsActive,
            ["isClosed"] = page.IsClosed,
        };
    }

    public static WebDialogReference RequiredDialogReference(JsonObject parameters)
    {
        JsonNode? node = parameters["dialog"];
        string? id = StringValue(node);
        if (id is null && node is JsonObject obj)
        {
            id = StringProperty(obj, "id") ?? StringProperty(obj, "dialogId");
        }

        return string.IsNullOrWhiteSpace(id) ? throw ParameterError("dialog", "Parameter 'dialog' must be a dialog reference.") : new WebDialogReference(id);
    }

    public static JsonObject DialogJson(WebDialogInformation dialog)
    {
        return new JsonObject
        {
            ["id"] = dialog.Reference.DialogId,
            ["kind"] = dialog.Kind.ToString(),
            ["message"] = dialog.Message,
        };
    }

    public static IReadOnlyList<WorkflowArtifactReference> ArtifactReferences(JsonObject parameters)
    {
        if (parameters["artifacts"] is JsonArray array)
        {
            return array.Select(static item => item is JsonObject obj ? ArtifactReference(obj) : throw ParameterError("artifacts", "Each artifact must be an object.")).ToArray();
        }

        return [ArtifactReference(RequiredObject(parameters, "artifact"))];
    }

    public static WorkflowArtifactReference ArtifactReference(JsonObject obj)
    {
        return new WorkflowArtifactReference(
            RequiredString(obj, "artifactId"),
            RequiredString(obj, "filename"),
            RequiredString(obj, "mediaType"),
            RequiredLong(obj, "size"),
            ParseSensitivity(StringProperty(obj, "sensitivity") ?? "internal"),
            StringProperty(obj, "sha256"));
    }

    public static JsonObject ArtifactJson(WorkflowArtifactReference artifact)
    {
        return new JsonObject
        {
            ["artifactId"] = artifact.ArtifactId,
            ["filename"] = artifact.Filename,
            ["mediaType"] = artifact.MediaType,
            ["size"] = artifact.Size,
            ["sensitivity"] = artifact.Sensitivity.ToString(),
            ["sha256"] = artifact.Sha256,
        };
    }

    public static IReadOnlyList<WebCookie> CookieReferences(JsonObject parameters)
    {
        if (parameters["cookies"] is JsonArray array)
        {
            return array.Select(static item => item is JsonObject obj ? CookieReference(obj) : throw ParameterError("cookies", "Each cookie must be an object.")).ToArray();
        }

        return [CookieReference(RequiredObject(parameters, "cookie"))];
    }

    public static WebCookie CookieReference(JsonObject obj)
    {
        return new WebCookie(
            RequiredString(obj, "name"),
            RequiredString(obj, "value"),
            RequiredString(obj, "domain"),
            StringProperty(obj, "path") ?? "/",
            OptionalDouble(obj, "expires"),
            StringProperty(obj, "sameSite") ?? "Lax",
            OptionalBool(obj, "httpOnly"),
            OptionalBool(obj, "secure"));
    }

    public static JsonObject CookieJson(WebCookie cookie)
    {
        return new JsonObject
        {
            ["name"] = cookie.Name,
            ["value"] = cookie.Value,
            ["domain"] = cookie.Domain,
            ["path"] = cookie.Path,
            ["expires"] = cookie.Expires,
            ["sameSite"] = cookie.SameSite,
            ["httpOnly"] = cookie.HttpOnly,
            ["secure"] = cookie.Secure,
        };
    }

    public static IReadOnlyList<string>? OptionalStringArray(JsonObject parameters, string name)
    {
        return parameters[name] is JsonArray array
            ? array.Select(static item => item?.GetValue<string>() ?? string.Empty).Where(static value => value.Length > 0).ToArray()
            : null;
    }

    public static JsonObject RequiredObject(JsonObject parameters, string name)
    {
        return parameters[name] as JsonObject ?? throw ParameterError(name, $"Parameter '{name}' must be an object.");
    }

    public static string RequiredString(JsonObject parameters, string name)
    {
        return StringProperty(parameters, name) ?? throw ParameterError(name, $"Parameter '{name}' must be a string.");
    }

    public static long RequiredLong(JsonObject parameters, string name)
    {
        if (parameters[name] is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
        {
            throw ParameterError(name, $"Parameter '{name}' must be a number.");
        }

        return value.TryGetValue(out long longValue)
            ? longValue
            : value.GetValue<int>();
    }

    public static double? OptionalDouble(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : null;
    }

    public static bool OptionalBool(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False && value.GetValue<bool>();
    }

    public static string? StringProperty(JsonObject parameters, string name)
    {
        return StringValue(parameters[name]);
    }

    public static string? StringValue(JsonNode? node)
    {
        return node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
    }

    public static WorkflowArtifactSensitivity ParseSensitivity(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "public" => WorkflowArtifactSensitivity.Public,
            "sensitive" => WorkflowArtifactSensitivity.Sensitive,
            _ => WorkflowArtifactSensitivity.Internal,
        };
    }

    public static WebAutomationException ParameterError(string operation, string message)
    {
        return new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebActionFailed, message, operation));
    }
}
