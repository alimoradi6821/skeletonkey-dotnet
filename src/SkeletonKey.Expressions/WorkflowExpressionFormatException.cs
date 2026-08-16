namespace SkeletonKey.Expressions;

/// <summary>
/// Represents a malformed reserved expression workflow-value wrapper.
/// </summary>
public sealed class WorkflowExpressionFormatException : FormatException
{
    /// <summary>
    /// Initializes a new expression wrapper format exception.
    /// </summary>
    /// <param name="jsonPath">The JSON Pointer path to the malformed wrapper.</param>
    /// <param name="message">A deterministic validation message.</param>
    public WorkflowExpressionFormatException(string jsonPath, string message)
        : base(message)
    {
        JsonPath = jsonPath;
    }

    /// <summary>
    /// Gets the JSON Pointer path to the malformed wrapper.
    /// </summary>
    public string JsonPath { get; }
}
