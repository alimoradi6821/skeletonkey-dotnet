using System.Text.Json.Nodes;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Executes the exact <c>core.return</c> built-in node contract.
/// </summary>
/// <remarks>
/// The handler succeeds without activating outgoing control ports and returns materialized terminal metadata for runtime aggregation.
/// It does not access resources, files, network, time, or randomness.
/// </remarks>
public sealed class CoreReturnHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("core.return", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        JsonObject metadata = new()
        {
            ["terminal"] = true,
        };

        if (request.Parameters["outcome"] is JsonObject outcome)
        {
            metadata["outcome"] = outcome.DeepClone();
        }

        return ValueTask.FromResult(NodeHandlerResult.Success(metadata: metadata));
    }
}
