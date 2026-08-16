namespace SkeletonKey.Analysis;

/// <summary>
/// Describes node capability compatibility analysis status.
/// </summary>
public enum WorkflowCapabilityCompatibilityStatus
{
    /// <summary>Capability compatibility was not analyzed.</summary>
    NotAnalyzed,

    /// <summary>Capabilities are compatible.</summary>
    Compatible,

    /// <summary>A required capability is missing.</summary>
    MissingRequiredCapability,

    /// <summary>Capabilities are incompatible.</summary>
    Incompatible,
}
