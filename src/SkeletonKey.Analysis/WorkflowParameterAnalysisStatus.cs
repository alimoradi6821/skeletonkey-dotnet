namespace SkeletonKey.Analysis;

/// <summary>
/// Describes catalog parameter contract analysis status for a workflow node.
/// </summary>
public enum WorkflowParameterAnalysisStatus
{
    /// <summary>Parameter contracts were not analyzed.</summary>
    NotAnalyzed,

    /// <summary>Parameters satisfy the catalog contract.</summary>
    Valid,

    /// <summary>Parameters do not satisfy the catalog contract.</summary>
    Invalid,

    /// <summary>No parameter schema is available.</summary>
    UnknownSchema,
}
