namespace SkeletonKey.Runtime;

/// <summary>
/// Defines runtime-owned workflow event kinds emitted during deterministic execution.
/// </summary>
public enum RuntimeWorkflowEventKind
{
    /// <summary>The root execution was created.</summary>
    ExecutionCreated,

    /// <summary>The root execution became ready.</summary>
    ExecutionReady,

    /// <summary>The root execution started running.</summary>
    ExecutionStarted,

    /// <summary>The root execution suspended for an in-memory continuation.</summary>
    ExecutionSuspended,

    /// <summary>The root execution resumed after an in-memory continuation or durable safe-boundary checkpoint.</summary>
    ExecutionResumed,

    /// <summary>A planned node step became ready.</summary>
    NodeReady,

    /// <summary>A planned node step started running.</summary>
    NodeStarted,

    /// <summary>A planned node step completed successfully.</summary>
    NodeCompleted,

    /// <summary>A planned node step failed.</summary>
    NodeFailed,

    /// <summary>A planned node step was cancelled.</summary>
    NodeCancelled,

    /// <summary>A planned node step was skipped because it was unreachable on the completed path.</summary>
    NodeSkipped,

    /// <summary>The root execution completed successfully.</summary>
    ExecutionCompleted,

    /// <summary>The root execution failed.</summary>
    ExecutionFailed,

    /// <summary>The root execution was cancelled.</summary>
    ExecutionCancelled,

    /// <summary>A handler-supplied log observation was accepted and sequenced by the runtime.</summary>
    NodeLog,

    /// <summary>A handler-supplied progress observation was accepted and sequenced by the runtime.</summary>
    NodeProgress,

    /// <summary>A handler-supplied output observation was accepted and sequenced by the runtime.</summary>
    NodeOutput,

    /// <summary>A failed node attempt scheduled another policy-controlled attempt.</summary>
    NodeRetryScheduled,

    /// <summary>A policy-controlled retry attempt started after its safe retry boundary.</summary>
    NodeRetryStarted,

    /// <summary>A node failure was consumed by an explicit continue policy.</summary>
    NodeErrorContinued,

    /// <summary>A node failure stopped execution through an explicit stop policy.</summary>
    NodeExecutionStopped,
}
