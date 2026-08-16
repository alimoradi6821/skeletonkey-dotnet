namespace SkeletonKey.Planning;

/// <summary>
/// Describes execution planning issue severity.
/// </summary>
public enum WorkflowExecutionPlanIssueSeverity
{
    /// <summary>
    /// The issue prevents producing a ready execution plan.
    /// </summary>
    Error,

    /// <summary>
    /// The issue is advisory and does not block plan creation.
    /// </summary>
    Warning,
}
