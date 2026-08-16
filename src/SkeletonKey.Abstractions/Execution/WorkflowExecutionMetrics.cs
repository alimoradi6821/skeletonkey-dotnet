namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Represents host-neutral final workflow execution metrics.
/// </summary>
/// <param name="NodesExecuted">The number of nodes executed.</param>
/// <param name="RecordsEmitted">The number of streamed records emitted.</param>
/// <param name="DurationMilliseconds">The execution duration in milliseconds.</param>
public readonly record struct WorkflowExecutionMetrics(
    int NodesExecuted,
    long RecordsEmitted,
    long DurationMilliseconds);
