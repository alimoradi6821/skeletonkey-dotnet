using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Artifacts;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.BuiltIns;

/// <summary>
/// Provides shared provider-neutral web handler behavior.
/// </summary>
public abstract class WebHandlerBase(string type) : INodeHandler
{
    private const int _maximumFrameDepth = 5;

    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new(type, 1);

    /// <inheritdoc />
    public async ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using INodeResourceLease lease = await context.Resources.AcquireAsync("page", cancellationToken).ConfigureAwait(false);
            IWebPageAdapter adapter = lease.Resource.GetRequiredAdapter<IWebPageAdapter>();
            return await ExecuteWebAsync(request, context, adapter, lease.Resource, cancellationToken).ConfigureAwait(false);
        }
        catch (WebAutomationException exception)
        {
            return NodeHandlerResult.Failure(new WorkflowError(exception.Error.Code, exception.Error.Message, request.Identity.NodeId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NodeHandlerResult.Cancelled(new WorkflowError(WebAutomationErrorCodes.BrowserOperationCancelled, "Browser operation was cancelled.", request.Identity.NodeId));
        }
        catch (InvalidOperationException exception) when (string.Equals(exception.Message, "The requested resource adapter is not available.", StringComparison.Ordinal))
        {
            return NodeHandlerResult.Failure(new WorkflowError(WebAutomationErrorCodes.PageResourceUnavailable, "Web page resource unavailable.", request.Identity.NodeId));
        }
        catch (InvalidOperationException exception)
        {
            return NodeHandlerResult.Failure(new WorkflowError(WebAutomationErrorCodes.WebActionFailed, exception.Message, request.Identity.NodeId));
        }
    }

    /// <summary>Executes the handler after the page resource has been acquired.</summary>
    protected virtual ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("This web handler does not implement execution.");
    }

    /// <summary>Executes the handler after the page resource has been acquired.</summary>
    protected virtual ValueTask<NodeHandlerResult> ExecuteWebAsync(NodeExecutionRequest request, INodeExecutionContext context, IWebPageAdapter adapter, INodeResourceHandle resource, CancellationToken cancellationToken)
    {
        return ExecuteWebAsync(request, context, adapter, cancellationToken);
    }

    /// <summary>Creates a successful result activating the continuation control output.</summary>
    protected static NodeHandlerResult Main(IReadOnlyDictionary<string, NodePortValueSet>? data = null)
    {
        return NodeHandlerResult.Success(new NodeHandlerOutputs(["continue"], data));
    }

    /// <summary>Gets a required locator by slot name.</summary>
    protected static ResolvedLocatorPlan RequiredLocator(INodeExecutionContext context, string slotName)
    {
        return context.Locators.TryGet(slotName, out ResolvedLocatorPlan? locator) && locator is not null
            ? locator
            : throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.LocatorNotFound, "Required locator slot is unavailable.", "locator"));
    }

    /// <summary>Reads a string parameter.</summary>
    protected static string RequiredString(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebActionFailed, $"Parameter '{name}' must be a string.", name));
    }

    /// <summary>Reads an optional string parameter.</summary>
    protected static string OptionalString(JsonObject parameters, string name, string fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : fallback;
    }

    /// <summary>Reads an optional integer parameter.</summary>
    protected static int OptionalInt(JsonObject parameters, string name, int fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<int>() : fallback;
    }

    /// <summary>Reads an optional nullable integer parameter.</summary>
    protected static int? OptionalNullableInt(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<int>() : null;
    }

    /// <summary>Reads an optional nullable long parameter.</summary>
    protected static long? OptionalNullableLong(JsonObject parameters, string name)
    {
        if (parameters[name] is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetValue(out long longValue)
            ? longValue
            : value.GetValue<int>();
    }

    /// <summary>Reads an optional boolean parameter.</summary>
    protected static bool OptionalBool(JsonObject parameters, string name, bool fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? value.GetValue<bool>() : fallback;
    }

    /// <summary>Reads an optional nullable string parameter.</summary>
    protected static string? OptionalNullableString(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
    }

    /// <summary>Reads a required boolean parameter.</summary>
    protected static bool RequiredBool(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? value.GetValue<bool>()
            : throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebActionFailed, $"Parameter '{name}' must be a boolean.", name));
    }

    /// <summary>Gets the artifact store exposed by the resource handle.</summary>
    protected static IWorkflowArtifactStore RequiredArtifactStore(INodeResourceHandle resource)
    {
        return resource.TryGetAdapter(out IWorkflowArtifactStore? store) && store is not null
            ? store
            : throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.ArtifactUnavailable, "Artifact store unavailable for this web resource.", "artifactStore"));
    }

    /// <summary>Builds the optional page and frame target context for one web operation.</summary>
    protected static WebTargetContext TargetContext(INodeExecutionContext context, JsonObject parameters)
    {
        WebPageReference? page = OptionalPageReference(parameters);
        List<ResolvedLocatorPlan> frames = [];
        if (parameters["frames"] is JsonArray array)
        {
            if (array.Count > _maximumFrameDepth)
            {
                throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.FrameCardinalityMismatch, "Frame target depth exceeds the supported maximum.", "frames"));
            }

            foreach (JsonNode? item in array)
            {
                string slotName = item is JsonValue value && value.GetValueKind() == JsonValueKind.String
                    ? value.GetValue<string>()
                    : throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebActionFailed, "Frame target entries must be locator slot names.", "frames"));
                if (!context.Locators.TryGet(slotName, out ResolvedLocatorPlan? locator) || locator is null)
                {
                    throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.FrameNotFound, "Declared frame locator slot is unavailable.", slotName));
                }

                frames.Add(locator);
            }
        }

        return new WebTargetContext(page, frames);
    }

    /// <summary>Reads a required page reference parameter.</summary>
    protected static WebPageReference RequiredPageReference(JsonObject parameters)
    {
        return OptionalPageReference(parameters) ?? throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebActionFailed, "Parameter 'page' must be a page reference.", "page"));
    }

    /// <summary>Reads an optional page reference parameter.</summary>
    protected static WebPageReference? OptionalPageReference(JsonObject parameters)
    {
        JsonNode? node = parameters["page"];
        if (node is null)
        {
            return null;
        }

        string? id = node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
        if (id is null && node is JsonObject obj)
        {
            id = OptionalNullableString(obj, "id") ?? OptionalNullableString(obj, "pageId");
        }

        return string.IsNullOrWhiteSpace(id)
            ? throw new WebAutomationException(new WebOperationError(WebAutomationErrorCodes.WebActionFailed, "Parameter 'page' must be a page reference.", "page"))
            : new WebPageReference(id);
    }

    /// <summary>Creates data output values from strings while preserving explicit nulls.</summary>
    protected static NodePortValueSet StringValues(IEnumerable<string?> values)
    {
        return new NodePortValueSet(values.Select(static value => value is null ? null : JsonValue.Create(value)));
    }

    /// <summary>Creates data output values from JSON nodes.</summary>
    protected static NodePortValueSet JsonValues(IEnumerable<JsonNode?> values)
    {
        return new NodePortValueSet(values);
    }
}
