namespace SkeletonKey.Runtime;

/// <summary>
/// Persists versioned workflow execution checkpoints with optimistic revision checks.
/// </summary>
/// <remarks>
/// Implementations are host-owned. The runtime never derives a store location from workflow data.
/// A save must replace the previous checkpoint atomically or leave it unchanged.
/// </remarks>
public interface IWorkflowCheckpointStore
{
    /// <summary>Loads the latest checkpoint for one caller-supplied execution identifier.</summary>
    public ValueTask<WorkflowExecutionCheckpoint?> LoadAsync(string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically saves a checkpoint when the current persisted revision equals <paramref name="expectedRevision"/>.
    /// </summary>
    public ValueTask SaveAsync(WorkflowExecutionCheckpoint checkpoint, long expectedRevision, CancellationToken cancellationToken = default);
}
