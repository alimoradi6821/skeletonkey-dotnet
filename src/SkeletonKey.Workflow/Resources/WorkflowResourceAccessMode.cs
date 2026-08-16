namespace SkeletonKey.Workflow.Resources;

/// <summary>
/// Describes whether a declared resource may be shared by concurrent consumers.
/// </summary>
public enum WorkflowResourceAccessMode
{
    /// <summary>
    /// Declares that future hosts must synchronize concurrent use or provide separate resource instances.
    /// </summary>
    Exclusive,

    /// <summary>
    /// Declares that future hosts may share one resolved resource among concurrent consumers.
    /// </summary>
    Shared,
}
