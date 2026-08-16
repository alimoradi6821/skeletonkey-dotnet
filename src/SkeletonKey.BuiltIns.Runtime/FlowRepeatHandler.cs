using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Evaluates the first activation decision for the exact <c>flow.repeat</c> built-in node contract.
/// </summary>
public sealed class FlowRepeatHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("flow.repeat", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        long count = request.Parameters["count"] is null ? 0 : request.Parameters["count"]!.GetValue<long>();
        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs([count > 0 ? "body" : "completed"])));
    }
}
