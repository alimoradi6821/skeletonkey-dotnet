using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Executes the exact <c>core.end</c> built-in node contract.
/// </summary>
/// <remarks>
/// The handler succeeds without activating control or data outputs. The execution planner marks the node as terminal, so a successful
/// completion ends the current workflow without requiring a synthetic outcome payload.
/// </remarks>
public sealed class CoreEndHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("core.end", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(NodeHandlerResult.Success());
    }
}
