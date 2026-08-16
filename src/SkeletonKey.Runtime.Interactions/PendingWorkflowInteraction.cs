using SkeletonKey.Abstractions.Interaction;

namespace SkeletonKey.Runtime.Interactions;

/// <summary>
/// Describes one process-local interaction request currently suspended inside a runtime execution session.
/// </summary>
public sealed class PendingWorkflowInteraction
{
    /// <summary>
    /// Initializes a pending interaction descriptor.
    /// </summary>
    /// <param name="continuationId">The session-local continuation identifier.</param>
    /// <param name="request">The materialized host-neutral interaction request.</param>
    /// <param name="createdAt">The runtime timestamp when the session suspended.</param>
    /// <param name="expiresAt">The optional runtime timestamp after which a continuation is timed out.</param>
    public PendingWorkflowInteraction(string continuationId, WorkflowInteractionRequest request, DateTimeOffset createdAt, DateTimeOffset? expiresAt = null)
    {
        ContinuationId = continuationId;
        Request = request ?? throw new ArgumentNullException(nameof(request));
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the session-local continuation identifier.</summary>
    public string ContinuationId { get; }

    /// <summary>Gets the materialized host-neutral interaction request.</summary>
    public WorkflowInteractionRequest Request { get; }

    /// <summary>Gets the runtime timestamp when the session suspended.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets the optional runtime timestamp after which a continuation is timed out.</summary>
    public DateTimeOffset? ExpiresAt { get; }
}
