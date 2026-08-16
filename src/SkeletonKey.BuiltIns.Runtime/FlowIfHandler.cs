using System.Text.Json;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Executes the exact <c>flow.if</c> built-in node contract.
/// </summary>
/// <remarks>
/// The handler reads the already-materialized boolean <c>condition</c> parameter and activates exactly one of <c>true</c> or <c>false</c>.
/// It does not evaluate expression syntax and does not access resources, files, network, time, or randomness.
/// </remarks>
public sealed class FlowIfHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("flow.if", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Parameters["condition"] is null || request.Parameters["condition"]!.GetValueKind() is not JsonValueKind.True and not JsonValueKind.False)
        {
            return ValueTask.FromResult(NodeHandlerResult.Failure(new WorkflowError("SKB1001", "flow.if requires a materialized boolean condition.", request.Identity.NodeId)));
        }

        bool condition = request.Parameters["condition"]!.GetValue<bool>();
        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs([condition ? "true" : "false"])));
    }
}
