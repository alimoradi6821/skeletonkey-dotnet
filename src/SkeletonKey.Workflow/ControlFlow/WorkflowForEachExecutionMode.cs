namespace SkeletonKey.Workflow.ControlFlow;

/// <summary>
/// Defines the execution mode contract for a graph-native foreach node.
/// </summary>
public enum WorkflowForEachExecutionMode
{
    /// <summary>
    /// Iterations run sequentially.
    /// </summary>
    Sequential,

    /// <summary>
    /// Iterations run with bounded concurrency.
    /// </summary>
    Parallel,
}
