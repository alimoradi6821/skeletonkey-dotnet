using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Executes the exact <c>core.start</c> built-in node contract.
/// </summary>
/// <remarks>
/// The handler succeeds, activates the <c>main</c> control output, produces no data, and does not access resources, files, network, time, or randomness.
/// </remarks>
public sealed class CoreStartHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("core.start", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["main"])));
    }
}
