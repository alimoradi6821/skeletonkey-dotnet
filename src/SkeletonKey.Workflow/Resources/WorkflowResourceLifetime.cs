namespace SkeletonKey.Workflow.Resources;

/// <summary>
/// Describes the future host lifetime requested for a resolved workflow resource.
/// </summary>
public enum WorkflowResourceLifetime
{
    /// <summary>
    /// A resolved resource may be shared across the complete root execution when explicitly mapped.
    /// </summary>
    Execution,

    /// <summary>
    /// A resolved resource belongs to one workflow invocation and is not inherited by child workflows.
    /// </summary>
    Invocation,
}
