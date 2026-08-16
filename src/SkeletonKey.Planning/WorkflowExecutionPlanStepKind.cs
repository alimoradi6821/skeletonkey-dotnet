namespace SkeletonKey.Planning;

/// <summary>
/// Describes the high-level role of a planned step.
/// </summary>
public enum WorkflowExecutionPlanStepKind
{
    /// <summary>
    /// Ordinary node step.
    /// </summary>
    Action,

    /// <summary>
    /// Workflow entry step.
    /// </summary>
    Entry,

    /// <summary>
    /// Terminal step.
    /// </summary>
    Terminal,

    /// <summary>
    /// Control-flow step.
    /// </summary>
    Control,

    /// <summary>
    /// Loop boundary step.
    /// </summary>
    Loop,

    /// <summary>
    /// Child workflow invocation step.
    /// </summary>
    Invocation,

    /// <summary>
    /// Human interaction step.
    /// </summary>
    Interaction,
}
