using SkeletonKey.Analysis;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Planning;

/// <summary>
/// Describes conversion from validated analysis into a host-neutral execution plan.
/// </summary>
public interface IWorkflowExecutionPlanner
{
    /// <summary>
    /// Plans workflow execution without running node handlers or resolving live resources.
    /// </summary>
    /// <param name="workflow">The workflow document to plan.</param>
    /// <param name="analysis">The catalog-aware analysis result for the workflow.</param>
    /// <returns>The immutable planning result.</returns>
    public WorkflowExecutionPlanResult Plan(WorkflowDocument workflow, WorkflowAnalysisResult analysis);
}
