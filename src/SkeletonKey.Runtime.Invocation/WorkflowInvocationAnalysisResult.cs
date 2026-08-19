namespace SkeletonKey.Runtime.Invocation;

/// <summary>Contains the immutable result of cross-workflow invocation analysis.</summary>
public sealed class WorkflowInvocationAnalysisResult
{
    /// <summary>Initializes an invocation analysis result.</summary>
    public WorkflowInvocationAnalysisResult(
        IReadOnlyList<WorkflowInvocationDependency>? dependencies = null,
        IReadOnlyList<WorkflowInvocationAnalysisIssue>? issues = null)
    {
        Dependencies = Array.AsReadOnly([.. (dependencies ?? Array.Empty<WorkflowInvocationDependency>())]);
        Issues = Array.AsReadOnly([.. (issues ?? Array.Empty<WorkflowInvocationAnalysisIssue>())]);
    }

    /// <summary>Gets a value indicating whether the full reachable invocation graph is valid.</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>Gets resolved dependencies in deterministic depth-first document order.</summary>
    public IReadOnlyList<WorkflowInvocationDependency> Dependencies { get; }

    /// <summary>Gets deterministic analysis errors.</summary>
    public IReadOnlyList<WorkflowInvocationAnalysisIssue> Issues { get; }
}
