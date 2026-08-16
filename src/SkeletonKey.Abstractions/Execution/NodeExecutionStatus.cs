namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Defines the final technical execution status of one node.
/// </summary>
public enum NodeExecutionStatus
{
    /// <summary>
    /// The node completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The node failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The node was intentionally skipped.
    /// </summary>
    Skipped,

    /// <summary>
    /// The node was cancelled before normal completion.
    /// </summary>
    Cancelled,
}
