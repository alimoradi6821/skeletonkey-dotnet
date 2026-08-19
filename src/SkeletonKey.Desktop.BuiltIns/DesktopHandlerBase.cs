using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Desktop.Abstractions;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;

namespace SkeletonKey.Desktop.BuiltIns;

/// <summary>Provides shared provider-neutral desktop handler behavior.</summary>
public abstract class DesktopHandlerBase(string type) : INodeHandler
{
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
            await using INodeResourceLease lease = await context.Resources.AcquireAsync("application", cancellationToken).ConfigureAwait(false);
            IDesktopApplicationAdapter adapter = lease.Resource.GetRequiredAdapter<IDesktopApplicationAdapter>();
            return await ExecuteDesktopAsync(request, context, adapter, cancellationToken).ConfigureAwait(false);
        }
        catch (DesktopAutomationException exception)
        {
            return NodeHandlerResult.Failure(new WorkflowError(exception.Error.Code, exception.Error.Message, request.Identity.NodeId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NodeHandlerResult.Cancelled(new WorkflowError(DesktopAutomationErrorCodes.DesktopOperationCancelled, "Desktop operation was cancelled.", request.Identity.NodeId));
        }
        catch (InvalidOperationException exception) when (string.Equals(exception.Message, "The requested resource adapter is not available.", StringComparison.Ordinal))
        {
            return NodeHandlerResult.Failure(new WorkflowError(DesktopAutomationErrorCodes.ApplicationResourceUnavailable, "Desktop application resource unavailable.", request.Identity.NodeId));
        }
        catch (InvalidOperationException exception)
        {
            return NodeHandlerResult.Failure(new WorkflowError(DesktopAutomationErrorCodes.DesktopActionFailed, exception.Message, request.Identity.NodeId));
        }
    }

    /// <summary>Executes the handler after the desktop application resource has been acquired.</summary>
    protected abstract ValueTask<NodeHandlerResult> ExecuteDesktopAsync(
        NodeExecutionRequest request,
        INodeExecutionContext context,
        IDesktopApplicationAdapter adapter,
        CancellationToken cancellationToken);

    /// <summary>Creates a successful result activating the continuation control output.</summary>
    protected static NodeHandlerResult Main(IReadOnlyDictionary<string, NodePortValueSet>? data = null)
    {
        return NodeHandlerResult.Success(new NodeHandlerOutputs(["continue"], data));
    }

    /// <summary>Gets the required desktop locator.</summary>
    protected static ResolvedLocatorPlan RequiredLocator(INodeExecutionContext context)
    {
        return context.Locators.TryGet("target", out ResolvedLocatorPlan? locator) && locator is not null
            ? locator
            : throw new DesktopAutomationException(new DesktopOperationError(DesktopAutomationErrorCodes.LocatorNotFound, "Required desktop locator is unavailable.", "locator"));
    }

    /// <summary>Reads a required string parameter.</summary>
    protected static string RequiredString(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new DesktopAutomationException(new DesktopOperationError(DesktopAutomationErrorCodes.DesktopActionFailed, $"Parameter '{name}' must be a string.", name));
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

    /// <summary>Creates data output values from strings while preserving explicit nulls.</summary>
    protected static NodePortValueSet StringValues(IEnumerable<string?> values)
    {
        return new NodePortValueSet(values.Select(static value => value is null ? null : JsonValue.Create(value)));
    }
}
