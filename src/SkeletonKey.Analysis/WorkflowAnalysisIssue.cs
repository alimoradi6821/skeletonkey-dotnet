namespace SkeletonKey.Analysis;

/// <summary>
/// Represents one catalog-aware static workflow analysis issue.
/// </summary>
public sealed class WorkflowAnalysisIssue
{
    /// <summary>
    /// Initializes an analysis issue.
    /// </summary>
    /// <param name="code">The stable analysis issue code.</param>
    /// <param name="severity">The issue severity.</param>
    /// <param name="message">A human-readable issue message.</param>
    /// <param name="path">The JSON Pointer path to the analyzed workflow location.</param>
    /// <param name="nodeId">Optional related node identifier.</param>
    /// <param name="nodeType">Optional related node type.</param>
    public WorkflowAnalysisIssue(
        string code,
        WorkflowAnalysisSeverity severity,
        string message,
        string path,
        string? nodeId = null,
        string? nodeType = null)
    {
        Code = code;
        Severity = severity;
        Message = message;
        Path = path;
        NodeId = nodeId;
        NodeType = nodeType;
    }

    /// <summary>
    /// Gets the stable analysis issue code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the issue severity.
    /// </summary>
    public WorkflowAnalysisSeverity Severity { get; }

    /// <summary>
    /// Gets a human-readable issue message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the JSON Pointer path to the analyzed workflow location.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets an optional related node identifier.
    /// </summary>
    public string? NodeId { get; }

    /// <summary>
    /// Gets an optional related node type.
    /// </summary>
    public string? NodeType { get; }
}
