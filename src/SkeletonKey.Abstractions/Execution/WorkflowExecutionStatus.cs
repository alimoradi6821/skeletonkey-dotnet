namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Defines the final technical execution status of a workflow.
/// </summary>
public enum WorkflowExecutionStatus
{
    /// <summary>
    /// The workflow engine completed execution normally.
    /// </summary>
    Succeeded,

    /// <summary>
    /// A technical or workflow execution failure prevented normal completion.
    /// </summary>
    Failed,

    /// <summary>
    /// Execution was cancelled before normal completion.
    /// </summary>
    Cancelled,
}
