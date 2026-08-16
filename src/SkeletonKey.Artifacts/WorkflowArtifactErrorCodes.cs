namespace SkeletonKey.Artifacts;

/// <summary>
/// Provides stable provider-neutral artifact error codes.
/// </summary>
public static class WorkflowArtifactErrorCodes
{
    /// <summary>Artifact was not found or is no longer available.</summary>
    public const string ArtifactUnavailable = "SKR2025";

    /// <summary>Artifact size exceeded a configured maximum.</summary>
    public const string ArtifactSizeLimitExceeded = "SKR2028";

    /// <summary>Artifact persistence failed.</summary>
    public const string ArtifactPersistenceFailed = "SKR2029";

    /// <summary>Artifact path validation failed.</summary>
    public const string ArtifactPathRejected = "SKR2040";
}
