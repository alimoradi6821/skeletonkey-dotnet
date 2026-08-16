namespace SkeletonKey.Binding;

/// <summary>
/// Represents a malformed structured workflow binding or literal wrapper.
/// </summary>
public sealed class WorkflowBindingFormatException : Exception
{
    /// <summary>
    /// Initializes a new binding format exception.
    /// </summary>
    /// <param name="jsonPath">The JSON Pointer path to the malformed value.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public WorkflowBindingFormatException(string jsonPath, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        JsonPath = jsonPath;
    }

    /// <summary>
    /// Gets the JSON Pointer path to the malformed value.
    /// </summary>
    public string JsonPath { get; }
}
