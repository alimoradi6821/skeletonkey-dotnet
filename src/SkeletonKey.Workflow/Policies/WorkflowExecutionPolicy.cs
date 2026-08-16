namespace SkeletonKey.Workflow.Policies;

/// <summary>
/// Declares future execution preferences for a workflow node.
/// </summary>
public sealed class WorkflowExecutionPolicy
{
    /// <summary>
    /// Initializes a new workflow execution policy declaration.
    /// </summary>
    /// <param name="timeout">The optional ISO-8601 timeout declaration.</param>
    /// <param name="onError">The declared error handling preference.</param>
    /// <param name="retry">The optional retry policy declaration.</param>
    public WorkflowExecutionPolicy(
        string? timeout = null,
        WorkflowOnError onError = WorkflowOnError.Fail,
        WorkflowRetryPolicy? retry = null)
    {
        Timeout = timeout;
        OnError = onError;
        Retry = retry;
    }

    /// <summary>
    /// Gets the optional ISO-8601 timeout declaration.
    /// </summary>
    public string? Timeout { get; }

    /// <summary>
    /// Gets the declared error handling preference.
    /// </summary>
    public WorkflowOnError OnError { get; }

    /// <summary>
    /// Gets the optional retry policy declaration.
    /// </summary>
    public WorkflowRetryPolicy? Retry { get; }
}
