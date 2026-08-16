namespace SkeletonKey.Validation;

/// <summary>
/// Represents one immutable semantic validation diagnostic.
/// </summary>
/// <remarks>
/// The <see cref="Path" /> property uses JSON Pointer syntax. Root-level issues use an empty string.
/// Validation issues do not contain exceptions.
/// </remarks>
public sealed class WorkflowValidationIssue
{
    /// <summary>
    /// Initializes a new semantic validation issue.
    /// </summary>
    /// <param name="code">The stable SKWxxxx validation code.</param>
    /// <param name="severity">The validation severity.</param>
    /// <param name="message">The human-readable diagnostic message.</param>
    /// <param name="path">The JSON Pointer path to the invalid declaration.</param>
    public WorkflowValidationIssue(
        string code,
        WorkflowValidationSeverity severity,
        string message,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(path);

        Code = code;
        Severity = severity;
        Message = message;
        Path = path;
    }

    /// <summary>
    /// Gets the stable SKWxxxx validation code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the validation severity.
    /// </summary>
    public WorkflowValidationSeverity Severity { get; }

    /// <summary>
    /// Gets the human-readable diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the JSON Pointer path to the invalid declaration, or an empty string for the root.
    /// </summary>
    public string Path { get; }
}
