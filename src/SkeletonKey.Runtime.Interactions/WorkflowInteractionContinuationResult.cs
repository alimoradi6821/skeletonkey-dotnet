using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Runtime.Interactions;

/// <summary>
/// Represents the result of applying a continuation to an in-memory interaction session.
/// </summary>
public sealed class WorkflowInteractionContinuationResult
{
    private WorkflowInteractionContinuationResult(bool accepted, WorkflowError? error)
    {
        Accepted = accepted;
        Error = error;
    }

    /// <summary>Gets a value indicating whether the continuation was accepted.</summary>
    public bool Accepted { get; }

    /// <summary>Gets the validation error for a rejected continuation.</summary>
    public WorkflowError? Error { get; }

    /// <summary>Creates an accepted continuation result.</summary>
    public static WorkflowInteractionContinuationResult Accept()
    {
        return new WorkflowInteractionContinuationResult(true, null);
    }

    /// <summary>Creates a rejected continuation result.</summary>
    public static WorkflowInteractionContinuationResult Reject(string code, string message)
    {
        return new WorkflowInteractionContinuationResult(false, new WorkflowError(code, message));
    }
}
