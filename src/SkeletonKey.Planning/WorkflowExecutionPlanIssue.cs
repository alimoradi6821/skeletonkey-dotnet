namespace SkeletonKey.Planning;

/// <summary>
/// Represents one execution planning issue.
/// </summary>
public sealed class WorkflowExecutionPlanIssue
{
    /// <summary>
    /// Initializes a planning issue.
    /// </summary>
    /// <param name="code">The stable planning issue code.</param>
    /// <param name="severity">The planning issue severity.</param>
    /// <param name="message">A human-readable issue message.</param>
    /// <param name="path">The JSON Pointer path to the related workflow location.</param>
    /// <param name="nodeId">Optional related node identifier.</param>
    public WorkflowExecutionPlanIssue(
        string code,
        WorkflowExecutionPlanIssueSeverity severity,
        string message,
        string path,
        string? nodeId = null)
    {
        Code = code;
        Severity = severity;
        Message = message;
        Path = path;
        NodeId = nodeId;
    }

    /// <summary>
    /// Gets the stable planning issue code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the planning issue severity.
    /// </summary>
    public WorkflowExecutionPlanIssueSeverity Severity { get; }

    /// <summary>
    /// Gets a human-readable issue message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the JSON Pointer path to the related workflow location.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets an optional related node identifier.
    /// </summary>
    public string? NodeId { get; }
}
