namespace SkeletonKey.Artifacts;

/// <summary>
/// Describes a controlled artifact write request.
/// </summary>
public sealed class WorkflowArtifactWriteRequest
{
    /// <summary>
    /// Initializes an artifact write request.
    /// </summary>
    public WorkflowArtifactWriteRequest(string filename, string mediaType, WorkflowArtifactSensitivity sensitivity = WorkflowArtifactSensitivity.Internal, long? maximumBytes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        Filename = filename;
        MediaType = mediaType;
        Sensitivity = sensitivity;
        MaximumBytes = maximumBytes;
    }

    /// <summary>Gets the logical filename requested by the caller.</summary>
    public string Filename { get; }

    /// <summary>Gets the media type associated with the content.</summary>
    public string MediaType { get; }

    /// <summary>Gets the sensitivity assigned to the artifact.</summary>
    public WorkflowArtifactSensitivity Sensitivity { get; }

    /// <summary>Gets an optional per-write maximum size in bytes.</summary>
    public long? MaximumBytes { get; }
}
