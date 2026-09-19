using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.BuiltIns;

/// <summary>Executes <c>web.extractCollection</c>.</summary>
public sealed class WebExtractCollectionHandler : WebHandlerBase
{
    private const int _maximumFields = 16;

    /// <summary>Initializes the handler.</summary>
    public WebExtractCollectionHandler() : base("web.extractCollection") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        IReadOnlyList<WebCollectionFieldDefinition> fields = ParseFields(p, context);
        int maximumItems = OptionalInt(p, "maximumItems", 100);
        int maximumOutputCharacters = OptionalInt(p, "maximumOutputCharacters", 262144);
        if (maximumItems is < 1 or > 1000)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebQueryFailed, "maximumItems must be between 1 and 1000.", "extractCollection"));
        }

        if (maximumOutputCharacters is < 1 or > 4 * 1024 * 1024)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebQueryFailed, "maximumOutputCharacters must be between 1 and 4194304.", "extractCollection"));
        }

        WebCollectionExtractionResult extracted = await adapter.ExtractCollectionAsync(
            RequiredLocator(context, "target"),
            fields,
            new WebCollectionExtractionRequest(
                TargetContext(context, p),
                OptionalInt(p, "timeoutMilliseconds", 30000),
                maximumItems,
                maximumOutputCharacters),
            cancellationToken).ConfigureAwait(false);

        List<JsonNode?> items = [];
        foreach (WebCollectionItem item in extracted.Items)
        {
            JsonObject output = [];
            foreach (WebCollectionFieldDefinition field in fields)
            {
                output[field.Name] = item.Fields.TryGetValue(field.Name, out string? value) && value is not null ? JsonValue.Create(value) : null;
            }

            items.Add(output);
        }

        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["items"] = JsonValues(items),
            ["totalMatchedCount"] = new([JsonValue.Create(extracted.TotalMatchedCount)]),
            ["truncated"] = new([JsonValue.Create(extracted.Truncated)]),
        });
    }

    private static IReadOnlyList<WebCollectionFieldDefinition> ParseFields(JsonObject parameters, INodeExecutionContext context)
    {
        if (parameters["fields"] is null)
        {
            return Array.AsReadOnly(Array.Empty<WebCollectionFieldDefinition>());
        }

        if (parameters["fields"] is not JsonArray array)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebQueryFailed, "fields must be an array.", "extractCollection"));
        }

        if (array.Count > _maximumFields)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebQueryFailed, "fields cannot contain more than 16 entries.", "extractCollection"));
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        List<WebCollectionFieldDefinition> fields = [];
        for (int index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject field)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebQueryFailed, "Each collection field must be an object.", "extractCollection"));
            }

            string name = RequiredString(field, "name");
            if (!names.Add(name))
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebQueryFailed, "Collection field names must be unique.", "extractCollection"));
            }

            string slotName = "field" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            ResolvedLocatorPlan locator = RequiredLocator(context, slotName);
            WebCollectionFieldReadMode mode = OptionalString(field, "mode", "text") switch
            {
                "attribute" => WebCollectionFieldReadMode.Attribute,
                "value" => WebCollectionFieldReadMode.Value,
                "text" => WebCollectionFieldReadMode.Text,
                _ => throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebQueryFailed, "Collection field mode must be text, attribute, or value.", "extractCollection")),
            };
            string? attributeName = mode == WebCollectionFieldReadMode.Attribute ? RequiredString(field, "attribute") : null;
            fields.Add(new WebCollectionFieldDefinition(
                name,
                locator,
                mode,
                attributeName,
                OptionalBool(field, "required", false),
                OptionalNullableInt(field, "elementIndex")));
        }

        return fields;
    }
}

/// <summary>Executes <c>web.scroll</c>.</summary>
public sealed class WebScrollHandler : WebSideEffectHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebScrollHandler() : base("web.scroll") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        ResolvedLocatorPlan? target = context.Locators.TryGet("target", out ResolvedLocatorPlan? found) ? found : null;
        WebScrollResult result = await adapter.ScrollAsync(
            target,
            new WebScrollRequest(
                OptionalDouble(p, "deltaX", 0),
                OptionalDouble(p, "deltaY", 0),
                OptionalInt(p, "timeoutMilliseconds", 30000),
                OptionalNullableInt(p, "elementIndex"),
                TargetContext(context, p)),
            cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["result"] = new([ScrollResult(result)]),
        });
    }

    internal static JsonObject ScrollResult(WebScrollResult result)
    {
        return new JsonObject
        {
            ["offsetX"] = result.OffsetX,
            ["offsetY"] = result.OffsetY,
            ["extentWidth"] = result.ExtentWidth,
            ["extentHeight"] = result.ExtentHeight,
            ["viewportWidth"] = result.ViewportWidth,
            ["viewportHeight"] = result.ViewportHeight,
            ["atHorizontalStart"] = result.AtHorizontalStart,
            ["atHorizontalEnd"] = result.AtHorizontalEnd,
            ["atVerticalStart"] = result.AtVerticalStart,
            ["atVerticalEnd"] = result.AtVerticalEnd,
        };
    }
}

/// <summary>Executes <c>web.scrollIntoView</c>.</summary>
public sealed class WebScrollIntoViewHandler : WebSideEffectHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebScrollIntoViewHandler() : base("web.scrollIntoView") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.ScrollIntoViewAsync(
            RequiredLocator(context, "target"),
            new WebElementActionRequest(OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)),
            cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.type</c>.</summary>
public sealed class WebTypeHandler : WebSideEffectHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebTypeHandler() : base("web.type") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        int delay = OptionalInt(p, "delayMilliseconds", 0);
        if (delay is < 0 or > 5000)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebActionFailed, "delayMilliseconds must be between 0 and 5000.", "type"));
        }

        await adapter.TypeAsync(
            RequiredLocator(context, "target"),
            new WebTypeRequest(RequiredString(p, "value"), delay, OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)),
            cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.insertText</c>.</summary>
public sealed class WebInsertTextHandler : WebSideEffectHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebInsertTextHandler() : base("web.insertText") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.InsertTextAsync(
            RequiredLocator(context, "target"),
            new WebInsertTextRequest(RequiredString(p, "value"), OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)),
            cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.clear</c>.</summary>
public sealed class WebClearHandler : WebSideEffectHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebClearHandler() : base("web.clear") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.ClearAsync(
            RequiredLocator(context, "target"),
            new WebElementActionRequest(OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)),
            cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.focus</c>.</summary>
public sealed class WebFocusHandler : WebSideEffectHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebFocusHandler() : base("web.focus") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        await adapter.FocusAsync(
            RequiredLocator(context, "target"),
            new WebElementActionRequest(OptionalInt(p, "timeoutMilliseconds", 30000), OptionalNullableInt(p, "elementIndex"), TargetContext(context, p)),
            cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>web.waitForCondition</c>.</summary>
public sealed class WebWaitForConditionHandler : WebHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public WebWaitForConditionHandler() : base("web.waitForCondition") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject p = request.Parameters;
        WebWaitConditionKind condition = RequiredString(p, "condition") switch
        {
            "exists" => WebWaitConditionKind.Exists,
            "visible" => WebWaitConditionKind.Visible,
            "hidden" => WebWaitConditionKind.Hidden,
            "textEquals" => WebWaitConditionKind.TextEquals,
            "textContains" => WebWaitConditionKind.TextContains,
            "attributeEquals" => WebWaitConditionKind.AttributeEquals,
            "countEquals" => WebWaitConditionKind.CountEquals,
            "countGreaterThan" => WebWaitConditionKind.CountGreaterThan,
            "valueEquals" => WebWaitConditionKind.ValueEquals,
            _ => throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.AdvancedWaitFailed, "Unsupported wait condition.", "waitForCondition")),
        };

        string? expectedValue = condition is WebWaitConditionKind.TextEquals or WebWaitConditionKind.TextContains or WebWaitConditionKind.AttributeEquals or WebWaitConditionKind.ValueEquals
            ? RequiredString(p, "expected")
            : null;
        string? attributeName = condition == WebWaitConditionKind.AttributeEquals ? RequiredString(p, "attribute") : null;
        int? expectedCount = condition is WebWaitConditionKind.CountEquals or WebWaitConditionKind.CountGreaterThan
            ? RequiredCount(p)
            : null;
        int pollInterval = OptionalInt(p, "pollIntervalMilliseconds", 50);
        if (pollInterval is < 10 or > 1000)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.AdvancedWaitFailed, "pollIntervalMilliseconds must be between 10 and 1000.", "waitForCondition"));
        }

        WebWaitConditionResult result = await adapter.WaitForConditionAsync(
            RequiredLocator(context, "target"),
            new WebWaitForConditionRequest(
                condition,
                expectedValue,
                attributeName,
                expectedCount,
                OptionalInt(p, "timeoutMilliseconds", 30000),
                pollInterval,
                OptionalNullableInt(p, "elementIndex"),
                TargetContext(context, p)),
            cancellationToken).ConfigureAwait(false);

        JsonObject output = new()
        {
            ["count"] = result.Count,
            ["actual"] = result.ActualValue,
        };
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["result"] = new([output]),
        });
    }

    private static int RequiredCount(JsonObject parameters)
    {
        if (parameters["count"] is not JsonValue value || value.GetValueKind() != JsonValueKind.Number || !value.TryGetValue(out int count) || count < 0)
        {
            throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.AdvancedWaitFailed, "count must be a non-negative integer.", "waitForCondition"));
        }

        return count;
    }
}
