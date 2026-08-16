namespace SkeletonKey.Abstractions.Interaction;

/// <summary>
/// Defines a host-neutral boundary for future human interaction requests.
/// </summary>
public interface IWorkflowInteractionHandler
{
    /// <summary>
    /// Requests a human response without prescribing UI, transport, persistence, or retry behavior.
    /// </summary>
    /// <param name="request">The immutable materialized interaction request.</param>
    /// <param name="cancellationToken">A token future runtimes pass through to the host handler.</param>
    /// <returns>The immutable response supplied by the host.</returns>
    public ValueTask<WorkflowInteractionResponse> RequestAsync(
        WorkflowInteractionRequest request,
        CancellationToken cancellationToken = default);
}
