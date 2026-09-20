using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Executes the exact <c>flow.merge</c> built-in node contract.
/// </summary>
/// <remarks>
/// <c>flow.merge</c> is an exclusive control-flow merge point: any compatible incoming
/// control activation may pass through the node and activate <c>continue</c>. It does not
/// wait for every configured incoming connection. Parallel synchronization remains a
/// separate concern.
/// </remarks>
public sealed class FlowMergeHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("flow.merge", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(
        NodeExecutionRequest request,
        INodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["continue"])));
    }
}
