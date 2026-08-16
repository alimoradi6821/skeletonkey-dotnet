using System.Text.Json.Nodes;

namespace SkeletonKey.Evaluation;

/// <summary>
/// Represents an immutable structured workflow value processing error.
/// </summary>
/// <remarks>
/// Errors contain stable codes and optional source spans. Metadata JSON is defensively cloned and stack traces are not part of this contract.
/// </remarks>
public sealed class WorkflowValueError
{
    private readonly JsonObject? _metadata;

    /// <summary>
    /// Initializes a new workflow value error.
    /// </summary>
    /// <param name="code">The stable SKV error code.</param>
    /// <param name="message">The deterministic error message.</param>
    /// <param name="jsonPath">The workflow JSON path associated with the error.</param>
    /// <param name="sourceOffset">The optional expression source offset.</param>
    /// <param name="sourceLength">The optional expression source length.</param>
    /// <param name="metadata">Optional structured metadata.</param>
    public WorkflowValueError(
        string code,
        string message,
        string jsonPath,
        int? sourceOffset = null,
        int? sourceLength = null,
        JsonObject? metadata = null)
    {
        Code = code;
        Message = message;
        JsonPath = jsonPath;
        SourceOffset = sourceOffset;
        SourceLength = sourceLength;
        _metadata = metadata is null ? null : (JsonObject)metadata.DeepClone();
    }

    /// <summary>
    /// Gets the stable SKV error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the deterministic error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the workflow JSON path associated with the error.
    /// </summary>
    public string JsonPath { get; }

    /// <summary>
    /// Gets the optional expression source offset.
    /// </summary>
    public int? SourceOffset { get; }

    /// <summary>
    /// Gets the optional expression source length.
    /// </summary>
    public int? SourceLength { get; }

    /// <summary>
    /// Gets a defensive copy of optional structured metadata.
    /// </summary>
    public JsonObject? Metadata => _metadata is null ? null : (JsonObject)_metadata.DeepClone();
}
