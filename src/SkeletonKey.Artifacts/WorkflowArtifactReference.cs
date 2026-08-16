namespace SkeletonKey.Artifacts;

/// <summary>
/// Represents an immutable opaque reference to an artifact owned by one artifact store.
/// </summary>
public sealed class WorkflowArtifactReference
{
    /// <summary>
    /// Initializes an artifact reference without exposing filesystem paths.
    /// </summary>
    public WorkflowArtifactReference(string artifactId, string filename, string mediaType, long size, WorkflowArtifactSensitivity sensitivity, string? sha256 = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Artifact size cannot be negative.");
        }

        ArtifactId = artifactId;
        Filename = filename;
        MediaType = mediaType;
        Size = size;
        Sensitivity = sensitivity;
        Sha256 = sha256;
    }

    /// <summary>Gets the opaque store-scoped artifact identifier.</summary>
    public string ArtifactId { get; }

    /// <summary>Gets the sanitized logical filename.</summary>
    public string Filename { get; }

    /// <summary>Gets the artifact media type.</summary>
    public string MediaType { get; }

    /// <summary>Gets the artifact byte size.</summary>
    public long Size { get; }

    /// <summary>Gets the artifact sensitivity.</summary>
    public WorkflowArtifactSensitivity Sensitivity { get; }

    /// <summary>Gets the optional lowercase hex SHA-256 hash.</summary>
    public string? Sha256 { get; }
}
