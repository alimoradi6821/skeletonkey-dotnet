namespace SkeletonKey.Catalog;

/// <summary>
/// Describes the stability of a catalog node definition.
/// </summary>
public enum WorkflowNodeStability
{
    /// <summary>
    /// Experimental contract.
    /// </summary>
    Experimental,

    /// <summary>
    /// Preview contract.
    /// </summary>
    Preview,

    /// <summary>
    /// Stable contract.
    /// </summary>
    Stable,
}
