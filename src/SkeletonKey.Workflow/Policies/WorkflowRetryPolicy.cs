namespace SkeletonKey.Workflow.Policies;

/// <summary>
/// Declares retry settings for runtime node execution.
/// </summary>
public sealed class WorkflowRetryPolicy
{
    /// <summary>
    /// Initializes a new workflow retry policy declaration.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of attempts to declare.</param>
    /// <param name="delay">The optional ISO-8601 delay between attempts.</param>
    /// <param name="backoff">The declared backoff multiplier.</param>
    /// <param name="maxDelay">The optional maximum ISO-8601 delay.</param>
    public WorkflowRetryPolicy(
        int maxAttempts = 1,
        string? delay = null,
        double backoff = 1.0,
        string? maxDelay = null)
    {
        MaxAttempts = maxAttempts;
        Delay = delay;
        Backoff = backoff;
        MaxDelay = maxDelay;
    }

    /// <summary>
    /// Gets the declared maximum attempt count.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the optional ISO-8601 delay between attempts.
    /// </summary>
    public string? Delay { get; }

    /// <summary>
    /// Gets the declared backoff multiplier.
    /// </summary>
    public double Backoff { get; }

    /// <summary>
    /// Gets the optional maximum ISO-8601 delay.
    /// </summary>
    public string? MaxDelay { get; }
}
