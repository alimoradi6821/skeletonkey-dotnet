namespace SkeletonKey.Planning;

/// <summary>
/// Represents the result of a workflow execution planning attempt.
/// </summary>
public sealed class WorkflowExecutionPlanResult
{
    private static readonly IReadOnlyList<WorkflowExecutionPlanIssue> _emptyIssues = Array.AsReadOnly(Array.Empty<WorkflowExecutionPlanIssue>());

    /// <summary>
    /// Initializes a planning result.
    /// </summary>
    /// <param name="workflowId">The planned workflow identifier.</param>
    /// <param name="status">Whether planning produced a ready plan contract.</param>
    /// <param name="plan">The execution plan, when planning succeeds.</param>
    /// <param name="issues">Planning issues in deterministic order.</param>
    public WorkflowExecutionPlanResult(
        string workflowId,
        WorkflowExecutionPlanStatus status,
        WorkflowExecutionPlan? plan = null,
        IReadOnlyList<WorkflowExecutionPlanIssue>? issues = null)
    {
        WorkflowId = workflowId;
        Status = status;
        Plan = plan;
        Issues = issues is null ? _emptyIssues : Array.AsReadOnly([.. issues]);
    }

    /// <summary>
    /// Gets the planned workflow identifier.
    /// </summary>
    public string WorkflowId { get; }

    /// <summary>
    /// Gets whether planning produced a ready plan contract.
    /// </summary>
    public WorkflowExecutionPlanStatus Status { get; }

    /// <summary>
    /// Gets the execution plan, when planning succeeds.
    /// </summary>
    public WorkflowExecutionPlan? Plan { get; }

    /// <summary>
    /// Gets planning issues in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowExecutionPlanIssue> Issues { get; }

    /// <summary>
    /// Gets error issues.
    /// </summary>
    public IEnumerable<WorkflowExecutionPlanIssue> Errors => Issues.Where(issue => issue.Severity == WorkflowExecutionPlanIssueSeverity.Error);

    /// <summary>
    /// Gets warning issues.
    /// </summary>
    public IEnumerable<WorkflowExecutionPlanIssue> Warnings => Issues.Where(issue => issue.Severity == WorkflowExecutionPlanIssueSeverity.Warning);

    /// <summary>
    /// Gets a value indicating whether a ready execution plan is available.
    /// </summary>
    public bool IsReady => Status == WorkflowExecutionPlanStatus.Ready && Plan is not null && !Errors.Any();
}
