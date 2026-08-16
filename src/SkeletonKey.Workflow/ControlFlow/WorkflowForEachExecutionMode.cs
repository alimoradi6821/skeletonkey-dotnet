namespace SkeletonKey.Workflow.ControlFlow;

/// <summary>
/// Defines the future execution mode contract for a graph-native foreach node.
/// </summary>
public enum WorkflowForEachExecutionMode
{
    /// <summary>
    /// Iterations are declared to run sequentially in a future runtime.
    /// </summary>
    Sequential,

    /// <summary>
    /// Iterations are declared to run in parallel in a future runtime.
    /// </summary>
    Parallel,
}
