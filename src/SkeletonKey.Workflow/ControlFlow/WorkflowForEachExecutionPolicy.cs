namespace SkeletonKey.Workflow.ControlFlow;

/// <summary>
/// Represents an immutable future execution policy for a graph-native foreach node.
/// </summary>
/// <remarks>
/// This contract only records requested sequential or parallel behavior. It does not schedule work,
/// aggregate results, or define runtime ordering.
/// </remarks>
public sealed class WorkflowForEachExecutionPolicy
{
    /// <summary>
    /// Initializes a new foreach execution policy.
    /// </summary>
    /// <param name="mode">The declared future execution mode.</param>
    /// <param name="maxConcurrency">The optional maximum concurrency for parallel mode.</param>
    public WorkflowForEachExecutionPolicy(
        WorkflowForEachExecutionMode mode = WorkflowForEachExecutionMode.Sequential,
        int? maxConcurrency = null)
    {
        Mode = mode;
        MaxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// Gets the declared future execution mode.
    /// </summary>
    public WorkflowForEachExecutionMode Mode { get; }

    /// <summary>
    /// Gets the optional maximum concurrency for parallel mode.
    /// </summary>
    public int? MaxConcurrency { get; }
}
