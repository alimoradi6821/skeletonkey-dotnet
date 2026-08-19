namespace SkeletonKey.Runtime.Invocation;

/// <summary>Defines deterministic limits for cross-workflow invocation analysis.</summary>
public sealed class WorkflowInvocationAnalysisOptions
{
    /// <summary>Initializes invocation analysis options.</summary>
    /// <param name="maximumDepth">The maximum child invocation depth below the root workflow.</param>
    public WorkflowInvocationAnalysisOptions(int maximumDepth = 64)
    {
        if (maximumDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth), maximumDepth, "The invocation analysis depth limit must be positive.");
        }

        MaximumDepth = maximumDepth;
    }

    /// <summary>Gets the maximum child invocation depth below the root workflow.</summary>
    public int MaximumDepth { get; }
}
