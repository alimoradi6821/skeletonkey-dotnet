namespace SkeletonKey.Runtime.Resources;

/// <summary>Captures provider-owned state at a durable runtime safe boundary.</summary>
public interface IWorkflowRuntimeResourceCheckpointParticipant
{
    /// <summary>
    /// Captures state that can reconstruct this resource, or returns null when the current resource state is not safely resumable.
    /// </summary>
    public ValueTask<WorkflowRuntimeResourceCheckpointState?> CaptureCheckpointStateAsync(CancellationToken cancellationToken = default);
}
