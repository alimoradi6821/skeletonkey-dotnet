using System.Text.Json.Nodes;
using SkeletonKey.Desktop.Abstractions;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.Desktop.BuiltIns;

/// <summary>Executes <c>desktop.click</c>.</summary>
public sealed class DesktopClickHandler : DesktopHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public DesktopClickHandler() : base("desktop.click") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteDesktopAsync(NodeExecutionRequest request, INodeExecutionContext context, IDesktopApplicationAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject parameters = request.Parameters;
        await adapter.ClickAsync(
            RequiredLocator(context),
            new DesktopClickRequest(OptionalString(parameters, "button", "left"), OptionalInt(parameters, "clickCount", 1), OptionalNullableInt(parameters, "timeoutMilliseconds"), OptionalNullableInt(parameters, "elementIndex")),
            cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>desktop.fill</c>.</summary>
public sealed class DesktopFillHandler : DesktopHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public DesktopFillHandler() : base("desktop.fill") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteDesktopAsync(NodeExecutionRequest request, INodeExecutionContext context, IDesktopApplicationAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject parameters = request.Parameters;
        await adapter.FillAsync(
            RequiredLocator(context),
            new DesktopFillRequest(RequiredString(parameters, "value"), OptionalNullableInt(parameters, "timeoutMilliseconds"), OptionalNullableInt(parameters, "elementIndex")),
            cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>desktop.press</c>.</summary>
public sealed class DesktopPressHandler : DesktopHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public DesktopPressHandler() : base("desktop.press") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteDesktopAsync(NodeExecutionRequest request, INodeExecutionContext context, IDesktopApplicationAdapter adapter, CancellationToken cancellationToken)
    {
        JsonObject parameters = request.Parameters;
        await adapter.PressAsync(
            RequiredLocator(context),
            new DesktopPressRequest(RequiredString(parameters, "key"), OptionalNullableInt(parameters, "timeoutMilliseconds"), OptionalNullableInt(parameters, "elementIndex")),
            cancellationToken).ConfigureAwait(false);
        return Main();
    }
}

/// <summary>Executes <c>desktop.getText</c>.</summary>
public sealed class DesktopGetTextHandler : DesktopHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public DesktopGetTextHandler() : base("desktop.getText") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteDesktopAsync(NodeExecutionRequest request, INodeExecutionContext context, IDesktopApplicationAdapter adapter, CancellationToken cancellationToken)
    {
        IReadOnlyList<string?> values = await adapter.GetTextAsync(
            RequiredLocator(context),
            new DesktopQueryRequest(OptionalNullableInt(request.Parameters, "timeoutMilliseconds"), OptionalNullableInt(request.Parameters, "elementIndex")),
            cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["result"] = StringValues(values) });
    }
}

/// <summary>Executes <c>desktop.getCount</c>.</summary>
public sealed class DesktopGetCountHandler : DesktopHandlerBase
{
    /// <summary>Initializes the handler.</summary>
    public DesktopGetCountHandler() : base("desktop.getCount") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteDesktopAsync(NodeExecutionRequest request, INodeExecutionContext context, IDesktopApplicationAdapter adapter, CancellationToken cancellationToken)
    {
        int count = await adapter.GetCountAsync(
            RequiredLocator(context),
            new DesktopQueryRequest(OptionalNullableInt(request.Parameters, "timeoutMilliseconds"), OptionalNullableInt(request.Parameters, "elementIndex")),
            cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal) { ["count"] = new([JsonValue.Create(count)]) });
    }
}
