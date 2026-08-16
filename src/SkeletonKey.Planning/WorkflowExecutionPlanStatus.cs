namespace SkeletonKey.Planning;

/// <summary>
/// Describes whether a planning attempt produced an executable plan contract.
/// </summary>
public enum WorkflowExecutionPlanStatus
{
    /// <summary>
    /// A plan contract was produced.
    /// </summary>
    Ready,

    /// <summary>
    /// Planning was blocked by validation, analysis, or catalog issues.
    /// </summary>
    Blocked,
}
