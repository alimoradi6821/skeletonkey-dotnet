namespace SkeletonKey.Execution;

/// <summary>
/// Describes the lifecycle state of a workflow execution, invocation, or node scope before final technical status is known.
/// </summary>
/// <remarks>
/// Lifecycle state is distinct from terminal execution status. A future runtime supplies transitions; this contract does not execute work.
/// </remarks>
public enum ExecutionLifecycleState
{
    /// <summary>
    /// The scope exists but has not been prepared.
    /// </summary>
    Created,

    /// <summary>
    /// The scope has passed preparation and may begin.
    /// </summary>
    Ready,

    /// <summary>
    /// The scope is actively executing or awaiting normal asynchronous work.
    /// </summary>
    Running,

    /// <summary>
    /// The scope is intentionally waiting for an external continuation.
    /// </summary>
    Suspended,

    /// <summary>
    /// Cancellation has been requested and cleanup may still be running.
    /// </summary>
    Cancelling,

    /// <summary>
    /// The lifecycle is terminal and the final technical status is represented by a result contract.
    /// </summary>
    Completed,
}
