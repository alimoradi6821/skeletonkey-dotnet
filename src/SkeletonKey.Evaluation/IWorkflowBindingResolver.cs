using SkeletonKey.Workflow.Bindings;

namespace SkeletonKey.Evaluation;

/// <summary>
/// Defines deterministic structured workflow binding resolution.
/// </summary>
public interface IWorkflowBindingResolver
{
    /// <summary>
    /// Resolves a workflow binding against an immutable value resolution context.
    /// </summary>
    /// <param name="binding">The structured binding declaration.</param>
    /// <param name="context">The immutable workflow value context.</param>
    /// <param name="jsonPath">The workflow JSON path associated with errors.</param>
    /// <returns>The resolved JSON value or a structured error.</returns>
    public WorkflowValueResult Resolve(
        WorkflowBinding binding,
        WorkflowValueResolutionContext context,
        string jsonPath);
}
