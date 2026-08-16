namespace SkeletonKey.Analysis;

/// <summary>
/// Describes whether a workflow endpoint matched catalog port metadata.
/// </summary>
public enum WorkflowPortCatalogStatus
{
    /// <summary>
    /// Port metadata was not analyzed.
    /// </summary>
    NotAnalyzed,

    /// <summary>
    /// The endpoint matched a catalog port with the expected direction.
    /// </summary>
    Known,

    /// <summary>
    /// The endpoint node was not catalog-known.
    /// </summary>
    UnknownNode,

    /// <summary>
    /// The endpoint node was known, but the port was not declared.
    /// </summary>
    UnknownPort,

    /// <summary>
    /// The endpoint matched a port with the opposite direction.
    /// </summary>
    WrongDirection,
}
