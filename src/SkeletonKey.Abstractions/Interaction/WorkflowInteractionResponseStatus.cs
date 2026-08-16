namespace SkeletonKey.Abstractions.Interaction;

/// <summary>
/// Describes the host-neutral status of a human interaction response.
/// </summary>
public enum WorkflowInteractionResponseStatus
{
    /// <summary>The human submitted a response.</summary>
    Submitted,

    /// <summary>The human cancelled the request.</summary>
    Cancelled,

    /// <summary>The request timed out.</summary>
    TimedOut,

    /// <summary>No interaction handler was available.</summary>
    Unavailable,
}
