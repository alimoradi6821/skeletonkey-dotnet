using System.Text.Json;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Evaluates the first activation decision for the exact <c>flow.while</c> built-in node contract.
/// </summary>
public sealed class FlowWhileHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("flow.while", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        string port = request.Parameters["condition"]?.GetValueKind() == JsonValueKind.True ? "body" : "completed";
        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs([port])));
    }
}
