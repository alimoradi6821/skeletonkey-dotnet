using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Evaluates the first activation decision for the exact <c>flow.foreach</c> built-in node contract.
/// </summary>
public sealed class FlowForEachHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("flow.foreach", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        JsonObject parameters = request.Parameters;
        string port = parameters["items"] is JsonArray items && items.Count > 0 ? "body" : "completed";
        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs([port])));
    }
}
