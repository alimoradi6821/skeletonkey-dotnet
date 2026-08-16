namespace SkeletonKey.Catalog;

/// <summary>
/// Describes whether a catalog port accepts incoming values or emits outgoing values.
/// </summary>
public enum WorkflowPortDirection
{
    /// <summary>
    /// A node input port.
    /// </summary>
    Input,

    /// <summary>
    /// A node output port.
    /// </summary>
    Output,
}
