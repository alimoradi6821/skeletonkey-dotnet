namespace SkeletonKey.Execution;

/// <summary>
/// Identifies the execution scope observed by a runtime state transition.
/// </summary>
public enum ExecutionScopeKind
{
    /// <summary>
    /// The transition describes the root workflow execution scope.
    /// </summary>
    Workflow,

    /// <summary>
    /// The transition describes one workflow invocation scope.
    /// </summary>
    Invocation,

    /// <summary>
    /// The transition describes one node execution attempt scope.
    /// </summary>
    Node,
}
