namespace SkeletonKey.Workflow.Bindings;

/// <summary>
/// Defines the local source kind for a structured workflow data binding.
/// </summary>
public enum WorkflowBindingSource
{
    /// <summary>
    /// Bind from a declared workflow input.
    /// </summary>
    Input,

    /// <summary>
    /// Bind from a declared workflow variable.
    /// </summary>
    Variable,

    /// <summary>
    /// Bind from a node output port.
    /// </summary>
    Node,

    /// <summary>
    /// Bind from an explicit iteration context identified by loop node ID.
    /// </summary>
    Iteration,
}
