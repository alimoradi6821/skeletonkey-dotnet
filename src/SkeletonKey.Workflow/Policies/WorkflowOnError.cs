namespace SkeletonKey.Workflow.Policies;

/// <summary>
/// Declares the runtime error-handling preference for a workflow node.
/// </summary>
public enum WorkflowOnError
{
    /// <summary>
    /// Stop execution by failing the workflow.
    /// </summary>
    Fail,

    /// <summary>
    /// Continue execution after the failed node.
    /// </summary>
    Continue,

    /// <summary>
    /// Stop execution without treating the stop as a successful continuation.
    /// </summary>
    Stop,
}
