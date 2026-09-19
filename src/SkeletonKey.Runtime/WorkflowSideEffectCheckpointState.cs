namespace SkeletonKey.Runtime;

/// <summary>Tracks durable dispatch certainty for an external side-effect node attempt.</summary>
public enum WorkflowSideEffectCheckpointState
{
    /// <summary>The node is not tracked as an external side effect.</summary>
    None,

    /// <summary>A durable boundary proves external dispatch has not started yet.</summary>
    NotDispatched,

    /// <summary>Dispatch may have started and the external outcome is not durably known.</summary>
    DispatchUncertain,

    /// <summary>The handler returned and the runtime durably knows the attempt completed locally.</summary>
    Completed,
}
