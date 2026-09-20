using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>Reads one host environment variable without introducing product-specific configuration semantics.</summary>
public sealed class EnvironmentVariableHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("core.environment", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string? name = request.Parameters["name"] is JsonValue nameValue && nameValue.GetValueKind() == JsonValueKind.String
            ? nameValue.GetValue<string>()
            : null;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
        {
            return ValueTask.FromResult(NodeHandlerResult.Failure(
                new WorkflowError("SKB1100", "core.environment requires a non-empty environment variable name of at most 256 characters.", request.Identity.NodeId)));
        }

        bool required = request.Parameters["required"] is not JsonValue requiredValue ||
            requiredValue.GetValueKind() is not JsonValueKind.False;
        string? fallback = request.Parameters["default"] is JsonValue fallbackValue && fallbackValue.GetValueKind() == JsonValueKind.String
            ? fallbackValue.GetValue<string>()
            : null;

        string? value = Environment.GetEnvironmentVariable(name);
        if (value is null)
        {
            if (fallback is not null)
            {
                value = fallback;
            }
            else if (required)
            {
                return ValueTask.FromResult(NodeHandlerResult.Failure(
                    new WorkflowError("SKB1101", "Required environment variable is not set.", request.Identity.NodeId)));
            }
            else
            {
                value = string.Empty;
            }
        }

        IReadOnlyDictionary<string, NodePortValueSet> data = new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["value"] = new([JsonValue.Create(value)]),
        };
        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["continue"], data)));
    }
}
