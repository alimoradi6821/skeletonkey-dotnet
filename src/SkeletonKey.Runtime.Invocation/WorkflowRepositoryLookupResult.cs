using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Runtime.Invocation;

/// <summary>
/// Represents the result of resolving a workflow invocation reference.
/// </summary>
public sealed class WorkflowRepositoryLookupResult
{
    private WorkflowRepositoryLookupResult(bool found, WorkflowDocument? workflow, string? diagnostic)
    {
        Found = found;
        Workflow = workflow;
        Diagnostic = diagnostic;
    }

    /// <summary>Gets a value indicating whether the workflow reference was resolved.</summary>
    public bool Found { get; }

    /// <summary>Gets the resolved workflow document when found.</summary>
    public WorkflowDocument? Workflow { get; }

    /// <summary>Gets optional host-neutral lookup diagnostic text.</summary>
    public string? Diagnostic { get; }

    /// <summary>Creates a successful lookup result.</summary>
    public static WorkflowRepositoryLookupResult Success(WorkflowDocument workflow)
    {
        return new WorkflowRepositoryLookupResult(true, workflow ?? throw new ArgumentNullException(nameof(workflow)), null);
    }

    /// <summary>Creates a not-found lookup result.</summary>
    public static WorkflowRepositoryLookupResult NotFound(string? diagnostic = null)
    {
        return new WorkflowRepositoryLookupResult(false, null, diagnostic);
    }
}
