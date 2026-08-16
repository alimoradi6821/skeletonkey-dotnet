using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Executes the exact <c>flow.switch</c> built-in node contract.
/// </summary>
/// <remarks>
/// Cases are evaluated in materialized declaration order. The first case whose materialized <c>when</c> value is <c>true</c> activates its exact
/// dynamic port; otherwise <c>default</c> is activated. The handler does not derive ports beyond materialized case IDs.
/// </remarks>
public sealed class FlowSwitchHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("flow.switch", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Parameters["cases"] is not JsonArray cases)
        {
            return ValueTask.FromResult(NodeHandlerResult.Failure(new WorkflowError("SKB1002", "flow.switch requires materialized cases.", request.Identity.NodeId)));
        }

        foreach (JsonNode? item in cases)
        {
            if (item is not JsonObject candidate || candidate["id"] is null || candidate["when"] is null)
            {
                return ValueTask.FromResult(NodeHandlerResult.Failure(new WorkflowError("SKB1003", "flow.switch case entries require materialized id and when values.", request.Identity.NodeId)));
            }

            if (candidate["when"]!.GetValueKind() is not JsonValueKind.True and not JsonValueKind.False)
            {
                return ValueTask.FromResult(NodeHandlerResult.Failure(new WorkflowError("SKB1004", "flow.switch case 'when' values must materialize to booleans.", request.Identity.NodeId)));
            }

            if (candidate["when"]!.GetValue<bool>())
            {
                return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs([candidate["id"]!.GetValue<string>()])));
            }
        }

        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["default"])));
    }
}
