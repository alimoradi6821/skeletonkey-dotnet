namespace SkeletonKey.Catalog;

/// <summary>
/// Describes high-level non-executable node behavior metadata.
/// </summary>
public enum WorkflowNodeBehaviorKind
{
    /// <summary>
    /// Ordinary node behavior.
    /// </summary>
    Action,

    /// <summary>
    /// Workflow entry behavior.
    /// </summary>
    Entry,

    /// <summary>
    /// Terminal workflow behavior.
    /// </summary>
    Terminal,

    /// <summary>
    /// Conditional branch behavior.
    /// </summary>
    Branch,

    /// <summary>
    /// Loop behavior.
    /// </summary>
    Loop,

    /// <summary>
    /// Child workflow invocation behavior.
    /// </summary>
    Invocation,

    /// <summary>
    /// Human interaction behavior.
    /// </summary>
    Interaction,
}
