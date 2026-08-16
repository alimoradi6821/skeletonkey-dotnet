namespace SkeletonKey.Analysis;

/// <summary>
/// Describes dynamic port analysis status for a workflow endpoint.
/// </summary>
public enum WorkflowDynamicPortAnalysisStatus
{
    /// <summary>Dynamic ports were not analyzed.</summary>
    NotAnalyzed,

    /// <summary>The endpoint does not use a dynamic port.</summary>
    NotDynamic,

    /// <summary>The dynamic port resolved successfully.</summary>
    Resolved,

    /// <summary>The dynamic port could not be resolved.</summary>
    Unresolved,

    /// <summary>The dynamic port declaration is invalid.</summary>
    InvalidDeclaration,
}
