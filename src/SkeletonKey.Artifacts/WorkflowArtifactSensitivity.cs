namespace SkeletonKey.Artifacts;

/// <summary>
/// Describes the sensitivity level of workflow-owned artifact content and metadata.
/// </summary>
public enum WorkflowArtifactSensitivity
{
    /// <summary>The artifact may be referenced in ordinary diagnostics.</summary>
    Public,

    /// <summary>The artifact is internal operational data and should not be exposed casually.</summary>
    Internal,

    /// <summary>The artifact contains sensitive data and paths/content must not be logged.</summary>
    Sensitive,
}
