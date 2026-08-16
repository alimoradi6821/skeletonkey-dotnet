namespace SkeletonKey.Runtime;

/// <summary>
/// Defines deterministic per-step scheduler status for one runtime execution.
/// </summary>
/// <remarks>
/// Step status is distinct from the external lifecycle state contracts and does not add persistence or durable suspension behavior.
/// </remarks>
public enum WorkflowStepRuntimeStatus
{
    /// <summary>The step is waiting for control and data dependencies.</summary>
    Pending,

    /// <summary>The step is ready to run.</summary>
    Ready,

    /// <summary>The step is currently running.</summary>
    Running,

    /// <summary>The step completed successfully.</summary>
    Succeeded,

    /// <summary>The step failed.</summary>
    Failed,

    /// <summary>The step was cancelled.</summary>
    Cancelled,

    /// <summary>The step was deterministically skipped as unreachable.</summary>
    Skipped,
}
