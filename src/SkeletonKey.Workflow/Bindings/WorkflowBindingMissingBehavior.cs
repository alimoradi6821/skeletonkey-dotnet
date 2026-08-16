namespace SkeletonKey.Workflow.Bindings;

/// <summary>
/// Defines how a future binding resolver should handle missing sources or paths.
/// </summary>
public enum WorkflowBindingMissingBehavior
{
    /// <summary>
    /// Missing data causes binding resolution to fail.
    /// </summary>
    Error,

    /// <summary>
    /// Missing data resolves to JSON null.
    /// </summary>
    Null,

    /// <summary>
    /// Missing data resolves to the explicit default JSON value.
    /// </summary>
    Default,
}
