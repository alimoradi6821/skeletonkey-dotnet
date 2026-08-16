namespace SkeletonKey.Workflow.Invocation;

/// <summary>
/// Defines how a workflow invocation declares child stream event visibility.
/// </summary>
public enum WorkflowInvocationStreamMode
{
    /// <summary>
    /// Child stream events remain visible under their original channel names.
    /// </summary>
    Forward,

    /// <summary>
    /// Child stream events are not forwarded beyond the invocation boundary.
    /// </summary>
    Suppress,

    /// <summary>
    /// Child stream channels are explicitly renamed into parent stream channels.
    /// </summary>
    Map,
}
