using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Policies;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers node execution policy declaration validation.
/// </summary>
public sealed class ExecutionPolicyValidationTests
{
    /// <summary>
    /// Verifies that valid timeout declarations are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidTimeout()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(timeout: "P1DT2H30M"));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidTimeout);
    }

    /// <summary>
    /// Verifies that zero timeout declarations are rejected.
    /// </summary>
    [Fact]
    public void RejectsZeroTimeout()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(timeout: "PT0S"));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidTimeout);
    }

    /// <summary>
    /// Verifies that negative timeout declarations are rejected.
    /// </summary>
    [Fact]
    public void RejectsNegativeTimeout()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(timeout: "PT-1S"));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidTimeout);
    }

    /// <summary>
    /// Verifies that invalid timeout syntax is rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidTimeoutSyntax()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(timeout: "30S"));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidTimeout);
    }

    /// <summary>
    /// Verifies that zero retry delay is accepted.
    /// </summary>
    [Fact]
    public void AcceptsZeroRetryDelay()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(delay: "PT0S")));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryDelay);
    }

    /// <summary>
    /// Verifies that fractional-second retry delay is accepted.
    /// </summary>
    [Fact]
    public void AcceptsFractionalSecondRetryDelay()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(delay: "PT0.5S")));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryDelay);
    }

    /// <summary>
    /// Verifies that negative retry delay is rejected.
    /// </summary>
    [Fact]
    public void RejectsNegativeRetryDelay()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(delay: "PT-1S")));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryDelay);
    }

    /// <summary>
    /// Verifies that retry maxAttempts below one is rejected.
    /// </summary>
    [Fact]
    public void RejectsRetryMaxAttemptsBelowOne()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(maxAttempts: 0)));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryAttemptCount);
    }

    /// <summary>
    /// Verifies that backoff equal to one is accepted.
    /// </summary>
    [Fact]
    public void AcceptsBackoffEqualToOne()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(backoff: 1.0)));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryBackoff);
    }

    /// <summary>
    /// Verifies that backoff below one is rejected.
    /// </summary>
    [Fact]
    public void RejectsBackoffBelowOne()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(backoff: 0.99)));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryBackoff);
    }

    /// <summary>
    /// Verifies that NaN backoff is rejected.
    /// </summary>
    [Fact]
    public void RejectsNaNBackoff()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(backoff: double.NaN)));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryBackoff);
    }

    /// <summary>
    /// Verifies that positive infinity backoff is rejected.
    /// </summary>
    [Fact]
    public void RejectsPositiveInfinityBackoff()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(backoff: double.PositiveInfinity)));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryBackoff);
    }

    /// <summary>
    /// Verifies that invalid maxDelay is rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidMaxDelay()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(maxDelay: "soon")));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidRetryMaximumDelay);
    }

    /// <summary>
    /// Verifies that maxDelay below delay is rejected.
    /// </summary>
    [Fact]
    public void RejectsMaxDelayBelowDelay()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(delay: "PT2S", maxDelay: "PT1S")));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.MaximumDelayLessThanDelay);
    }

    /// <summary>
    /// Verifies that maxDelay equal to delay is accepted.
    /// </summary>
    [Fact]
    public void AcceptsMaxDelayEqualToDelay()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(delay: "PT2S", maxDelay: "PT2S")));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.MaximumDelayLessThanDelay);
    }

    /// <summary>
    /// Verifies that maxDelay greater than delay is accepted.
    /// </summary>
    [Fact]
    public void AcceptsMaxDelayGreaterThanDelay()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(retry: new WorkflowRetryPolicy(delay: "PT2S", maxDelay: "PT30S")));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.MaximumDelayLessThanDelay);
    }

    /// <summary>
    /// Verifies that calendar month durations are rejected.
    /// </summary>
    [Fact]
    public void RejectsCalendarMonthDuration()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(timeout: "P1M"));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidTimeout);
    }

    /// <summary>
    /// Verifies that calendar year durations are rejected.
    /// </summary>
    [Fact]
    public void RejectsCalendarYearDuration()
    {
        WorkflowValidationResult result = ValidatePolicy(new WorkflowExecutionPolicy(timeout: "P1Y"));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidTimeout);
    }

    private static WorkflowValidationResult ValidatePolicy(WorkflowExecutionPolicy policy)
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1, policy: policy),
            ValidationTestData.Node("end", type: "core.end"),
        ];

        WorkflowDocument workflow = ValidationTestData.CreateValidWorkflow(nodes: nodes);
        return ValidationTestData.Validate(workflow);
    }
}
