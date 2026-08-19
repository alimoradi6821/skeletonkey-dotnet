namespace SkeletonKey.Workflow.ControlFlow;

/// <summary>
/// Represents an immutable execution policy for a graph-native foreach node.
/// </summary>
/// <remarks>
/// The runtime uses this contract to select sequential or bounded-parallel iteration scheduling.
/// It does not define distributed scheduling or result aggregation.
/// </remarks>
public sealed class WorkflowForEachExecutionPolicy
{
    /// <summary>
    /// Initializes a new foreach execution policy.
    /// </summary>
    /// <param name="mode">The declared execution mode.</param>
    /// <param name="maxConcurrency">The optional maximum concurrency for parallel mode.</param>
    public WorkflowForEachExecutionPolicy(
        WorkflowForEachExecutionMode mode = WorkflowForEachExecutionMode.Sequential,
        int? maxConcurrency = null)
    {
        Mode = mode;
        MaxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// Gets the declared execution mode.
    /// </summary>
    public WorkflowForEachExecutionMode Mode { get; }

    /// <summary>
    /// Gets the optional maximum concurrency for parallel mode.
    /// </summary>
    public int? MaxConcurrency { get; }
}
