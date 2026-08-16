namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Defines the business outcome kind for a completed workflow operation.
/// </summary>
public enum WorkflowOutcomeKind
{
    /// <summary>
    /// The intended business operation completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The workflow completed, but only part of the requested business operation succeeded.
    /// </summary>
    Partial,

    /// <summary>
    /// Human or external action is required.
    /// </summary>
    RequiresAction,

    /// <summary>
    /// The workflow completed successfully but produced no business records.
    /// </summary>
    NoResults,

    /// <summary>
    /// The operation was intentionally not performed.
    /// </summary>
    Skipped,
}
