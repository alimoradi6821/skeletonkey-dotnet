using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Handlers;

/// <summary>
/// Represents the immutable lightweight result returned by a node handler.
/// </summary>
/// <remarks>
/// This contract contains no execution IDs, timestamps, sequence numbers, or metrics. The runtime converts it into a full node execution result.
/// Metadata JSON is defensively cloned. Unexpected implementation exceptions are runtime faults, not values produced by this type.
/// </remarks>
public sealed class NodeHandlerResult
{
    private readonly JsonObject? _metadata;

    /// <summary>
    /// Initializes a new node handler result.
    /// </summary>
    /// <param name="status">The handler completion status.</param>
    /// <param name="outputs">The control and data outputs reported by the handler.</param>
    /// <param name="error">The optional structured expected failure or cancellation error.</param>
    /// <param name="metadata">Optional host-neutral diagnostic metadata.</param>
    /// <exception cref="ArgumentException">Thrown when a failed result does not include an error.</exception>
    public NodeHandlerResult(
        NodeHandlerCompletionStatus status,
        NodeHandlerOutputs? outputs = null,
        WorkflowError? error = null,
        JsonObject? metadata = null)
    {
        if (status == NodeHandlerCompletionStatus.Failed && error is null)
        {
            throw new ArgumentException("Failed handler results require a structured workflow error.", nameof(error));
        }

        Status = status;
        Outputs = outputs ?? new NodeHandlerOutputs();
        Error = error;
        _metadata = metadata is null ? null : (JsonObject)metadata.DeepClone();
    }

    /// <summary>
    /// Gets the handler completion status.
    /// </summary>
    public NodeHandlerCompletionStatus Status { get; }

    /// <summary>
    /// Gets the immutable control and data outputs reported by the handler.
    /// </summary>
    public NodeHandlerOutputs Outputs { get; }

    /// <summary>
    /// Gets the optional structured expected failure or cancellation error.
    /// </summary>
    public WorkflowError? Error { get; }

    /// <summary>
    /// Gets a defensive copy of optional host-neutral diagnostic metadata.
    /// </summary>
    public JsonObject? Metadata => _metadata is null ? null : (JsonObject)_metadata.DeepClone();

    /// <summary>
    /// Creates a successful handler result.
    /// </summary>
    /// <param name="outputs">The optional control and data outputs reported by the handler.</param>
    /// <param name="metadata">Optional host-neutral diagnostic metadata.</param>
    /// <returns>An immutable successful handler result.</returns>
    public static NodeHandlerResult Success(NodeHandlerOutputs? outputs = null, JsonObject? metadata = null)
    {
        return new NodeHandlerResult(NodeHandlerCompletionStatus.Succeeded, outputs, metadata: metadata);
    }

    /// <summary>
    /// Creates an expected failed handler result with a structured workflow error.
    /// </summary>
    /// <param name="error">The structured expected failure.</param>
    /// <param name="outputs">Optional outputs retained by explicit contract.</param>
    /// <param name="metadata">Optional host-neutral diagnostic metadata.</param>
    /// <returns>An immutable failed handler result.</returns>
    public static NodeHandlerResult Failure(WorkflowError error, NodeHandlerOutputs? outputs = null, JsonObject? metadata = null)
    {
        return new NodeHandlerResult(NodeHandlerCompletionStatus.Failed, outputs, error, metadata);
    }

    /// <summary>
    /// Creates a cancelled handler result.
    /// </summary>
    /// <param name="error">Optional structured cancellation error.</param>
    /// <param name="metadata">Optional host-neutral diagnostic metadata.</param>
    /// <returns>An immutable cancelled handler result.</returns>
    public static NodeHandlerResult Cancelled(WorkflowError? error = null, JsonObject? metadata = null)
    {
        return new NodeHandlerResult(NodeHandlerCompletionStatus.Cancelled, error: error, metadata: metadata);
    }
}
