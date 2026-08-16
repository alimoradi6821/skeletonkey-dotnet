namespace SkeletonKey.Analysis;

/// <summary>
/// Describes whether a workflow node matched a catalog definition.
/// </summary>
public enum WorkflowNodeCatalogStatus
{
    /// <summary>
    /// The node type and version matched an exact catalog definition.
    /// </summary>
    Known,

    /// <summary>
    /// No catalog definition exists for the node type.
    /// </summary>
    UnknownType,

    /// <summary>
    /// The node type exists, but the requested version is unavailable.
    /// </summary>
    UnknownVersion,
}
