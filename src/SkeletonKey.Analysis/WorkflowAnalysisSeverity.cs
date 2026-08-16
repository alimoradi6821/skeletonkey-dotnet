namespace SkeletonKey.Analysis;

/// <summary>
/// Describes the severity of a catalog-aware workflow analysis issue.
/// </summary>
public enum WorkflowAnalysisSeverity
{
    /// <summary>
    /// The issue blocks execution planning.
    /// </summary>
    Error,

    /// <summary>
    /// The issue is advisory and does not block planning.
    /// </summary>
    Warning,
}
