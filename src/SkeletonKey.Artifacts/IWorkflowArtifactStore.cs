namespace SkeletonKey.Artifacts;

/// <summary>
/// Defines a provider-neutral store for workflow-owned artifacts without exposing arbitrary filesystem paths.
/// </summary>
public interface IWorkflowArtifactStore
{
    /// <summary>
    /// Writes content into the store and returns an opaque reference.
    /// </summary>
    public ValueTask<WorkflowArtifactReference> WriteAsync(WorkflowArtifactWriteRequest request, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read-only stream for a previously created artifact.
    /// </summary>
    public ValueTask<Stream> OpenReadAsync(WorkflowArtifactReference reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets immutable metadata for a previously created artifact.
    /// </summary>
    public ValueTask<WorkflowArtifactMetadata> GetMetadataAsync(WorkflowArtifactReference reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a previously created artifact if it still exists.
    /// </summary>
    public ValueTask DeleteAsync(WorkflowArtifactReference reference, CancellationToken cancellationToken = default);
}
