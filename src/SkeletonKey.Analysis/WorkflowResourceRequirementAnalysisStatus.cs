namespace SkeletonKey.Analysis;

/// <summary>
/// Describes catalog resource requirement analysis status for a workflow node.
/// </summary>
public enum WorkflowResourceRequirementAnalysisStatus
{
    /// <summary>Resource requirements were not analyzed.</summary>
    NotAnalyzed,

    /// <summary>Resource requirements are satisfied.</summary>
    Satisfied,

    /// <summary>A required resource is missing.</summary>
    MissingRequiredResource,

    /// <summary>A resource kind is incompatible.</summary>
    IncompatibleResourceKind,

    /// <summary>A required resource capability is missing.</summary>
    MissingRequiredCapability,
}
