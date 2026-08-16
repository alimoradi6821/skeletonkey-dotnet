namespace SkeletonKey.Planning;

/// <summary>
/// Describes the kind of dependency between planned steps.
/// </summary>
public enum WorkflowExecutionPlanDependencyKind
{
    /// <summary>
    /// Control dependency.
    /// </summary>
    Control,

    /// <summary>
    /// Data dependency.
    /// </summary>
    Data,

    /// <summary>
    /// Resource ordering dependency.
    /// </summary>
    Resource,
}
