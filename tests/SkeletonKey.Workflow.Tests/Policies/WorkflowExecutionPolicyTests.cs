using SkeletonKey.Workflow.Policies;

namespace SkeletonKey.Workflow.Tests.Policies;

/// <summary>
/// Covers workflow execution policy declarations.
/// </summary>
public sealed class WorkflowExecutionPolicyTests
{
    /// <summary>
    /// Verifies error handling defaults to fail.
    /// </summary>
    [Fact]
    public void DefaultsOnErrorToFail()
    {
        WorkflowExecutionPolicy policy = new();

        Assert.Equal(WorkflowOnError.Fail, policy.OnError);
    }

    /// <summary>
    /// Verifies timeout text is preserved without parsing.
    /// </summary>
    [Fact]
    public void AllowsTimeoutDeclaration()
    {
        WorkflowExecutionPolicy policy = new(timeout: "PT30S");

        Assert.Equal("PT30S", policy.Timeout);
    }

    /// <summary>
    /// Verifies retry policy declarations are preserved.
    /// </summary>
    [Fact]
    public void PreservesRetryPolicyDeclaration()
    {
        WorkflowRetryPolicy retry = new(maxAttempts: 3, delay: "PT1S", backoff: 2.0, maxDelay: "PT10S");
        WorkflowExecutionPolicy policy = new(retry: retry);

        Assert.Same(retry, policy.Retry);
    }

    /// <summary>
    /// Verifies retry policy defaults to one attempt.
    /// </summary>
    [Fact]
    public void RetryPolicyDefaultsToOneAttempt()
    {
        WorkflowRetryPolicy retry = new();

        Assert.Equal(1, retry.MaxAttempts);
    }

    /// <summary>
    /// Verifies retry policy defaults backoff to one.
    /// </summary>
    [Fact]
    public void RetryPolicyDefaultsBackoffToOne()
    {
        WorkflowRetryPolicy retry = new();

        Assert.Equal(1.0, retry.Backoff);
    }
}
