using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Represents a host-neutral technical workflow or node execution error.
/// </summary>
/// <remarks>
/// The contract stores serializable error data only. Exceptions and stack traces are host logging concerns.
/// JSON details are defensively cloned on input and output.
/// </remarks>
public sealed class WorkflowError
{
    private readonly JsonObject? _details;

    /// <summary>
    /// Initializes a new workflow error.
    /// </summary>
    /// <param name="code">The technical error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="nodeId">The optional node identifier associated with the error.</param>
    /// <param name="retryable">Whether the error may be retryable.</param>
    /// <param name="details">Optional JSON error details.</param>
    public WorkflowError(
        string code,
        string message,
        string? nodeId = null,
        bool retryable = false,
        JsonObject? details = null)
    {
        Code = code;
        Message = message;
        NodeId = nodeId;
        Retryable = retryable;
        _details = details is null ? null : (JsonObject)details.DeepClone();
    }

    /// <summary>
    /// Gets the technical error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional node identifier associated with the error.
    /// </summary>
    public string? NodeId { get; }

    /// <summary>
    /// Gets a value indicating whether the error may be retryable.
    /// </summary>
    public bool Retryable { get; }

    /// <summary>
    /// Gets a defensive copy of optional JSON error details.
    /// </summary>
    public JsonObject? Details => _details is null ? null : (JsonObject)_details.DeepClone();
}
