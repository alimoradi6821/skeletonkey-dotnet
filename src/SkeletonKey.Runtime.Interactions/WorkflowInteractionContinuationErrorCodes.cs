namespace SkeletonKey.Runtime.Interactions;

/// <summary>
/// Defines stable error codes for in-memory interaction continuation validation.
/// </summary>
public static class WorkflowInteractionContinuationErrorCodes
{
    /// <summary>The supplied continuation identifier is unknown to the session.</summary>
    public const string UnknownContinuation = "SKI1001";

    /// <summary>The supplied continuation was already consumed.</summary>
    public const string ContinuationAlreadyCompleted = "SKI1002";

    /// <summary>The supplied continuation arrived after its timeout boundary.</summary>
    public const string ContinuationTimedOut = "SKI1003";

    /// <summary>The session was cancelled while suspended.</summary>
    public const string SessionCancelled = "SKI1004";
}
