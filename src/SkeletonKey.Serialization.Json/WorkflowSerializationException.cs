namespace SkeletonKey.Serialization.Json;

/// <summary>
/// Represents a failure while reading, writing, serializing, or deserializing workflow JSON.
/// </summary>
public sealed class WorkflowSerializationException : Exception
{
    /// <summary>
    /// Initializes a new workflow serialization exception.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="message">The formatted error message.</param>
    /// <param name="jsonPath">The best available JSON Pointer path.</param>
    /// <param name="lineNumber">The best available zero-based JSON line number.</param>
    /// <param name="bytePositionInLine">The best available zero-based byte position in the line.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public WorkflowSerializationException(
        WorkflowSerializationOperation operation,
        string message,
        string? jsonPath = null,
        long? lineNumber = null,
        long? bytePositionInLine = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        JsonPath = jsonPath;
        LineNumber = lineNumber;
        BytePositionInLine = bytePositionInLine;
    }

    /// <summary>
    /// Gets the best available JSON Pointer path for the failure.
    /// </summary>
    public string? JsonPath { get; }

    /// <summary>
    /// Gets the best available zero-based JSON line number for the failure.
    /// </summary>
    public long? LineNumber { get; }

    /// <summary>
    /// Gets the best available zero-based byte position in the line for the failure.
    /// </summary>
    public long? BytePositionInLine { get; }

    /// <summary>
    /// Gets the operation that failed.
    /// </summary>
    public WorkflowSerializationOperation Operation { get; }
}
