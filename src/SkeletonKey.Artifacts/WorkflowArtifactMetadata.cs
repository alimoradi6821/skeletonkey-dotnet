namespace SkeletonKey.Artifacts;

/// <summary>
/// Represents immutable metadata for a stored workflow artifact.
/// </summary>
public sealed class WorkflowArtifactMetadata
{
    /// <summary>
    /// Initializes artifact metadata.
    /// </summary>
    public WorkflowArtifactMetadata(WorkflowArtifactReference reference, DateTimeOffset createdAt)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        CreatedAt = createdAt;
    }

    /// <summary>Gets the artifact reference.</summary>
    public WorkflowArtifactReference Reference { get; }

    /// <summary>Gets the artifact creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; }
}
