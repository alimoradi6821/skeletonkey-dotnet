namespace SkeletonKey.Workflow.Outputs;

/// <summary>
/// Defines how a workflow output is produced.
/// </summary>
public enum WorkflowOutputMode
{
    /// <summary>
    /// A single final value produced from one node output port.
    /// </summary>
    Single,

    /// <summary>
    /// A final collection produced from one node output port.
    /// </summary>
    Collection,

    /// <summary>
    /// A named stream channel that may publish records during workflow execution.
    /// </summary>
    Stream,
}
