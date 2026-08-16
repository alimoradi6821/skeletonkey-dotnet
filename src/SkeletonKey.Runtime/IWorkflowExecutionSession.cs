using SkeletonKey.Runtime.Interactions;

namespace SkeletonKey.Runtime;

/// <summary>
/// Represents one in-memory runtime execution session that may complete, suspend for interaction, continue, or be cancelled.
/// </summary>
/// <remarks>
/// Sessions are intentionally process-local. They do not provide durable persistence, checkpointing, distributed coordination, retry behavior, or host UI.
/// </remarks>
public interface IWorkflowExecutionSession : IAsyncDisposable
{
    /// <summary>Gets the root execution identifier.</summary>
    public string ExecutionId { get; }

    /// <summary>Gets pending in-memory interactions owned by this session.</summary>
    public ValueTask<IReadOnlyList<PendingWorkflowInteraction>> GetPendingInteractionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Continues a pending in-memory interaction with a host response.</summary>
    public ValueTask<WorkflowInteractionContinuationResult> ContinueAsync(WorkflowInteractionContinuation continuation, CancellationToken cancellationToken = default);

    /// <summary>Cancels the in-memory session and any pending interaction waiters.</summary>
    public ValueTask CancelAsync(CancellationToken cancellationToken = default);

    /// <summary>Waits for the final runtime result.</summary>
    public ValueTask<WorkflowRuntimeResult> WaitForCompletionAsync(CancellationToken cancellationToken = default);
}
