using SkeletonKey.Catalog;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Analysis;

/// <summary>
/// Describes catalog-aware static workflow analysis.
/// </summary>
public interface IWorkflowAnalyzer
{
    /// <summary>
    /// Analyzes a workflow document against a supplied node definition catalog without executing the workflow.
    /// </summary>
    /// <param name="workflow">The workflow document to analyze.</param>
    /// <param name="catalog">The catalog used to resolve node definitions and ports.</param>
    /// <returns>The immutable analysis result.</returns>
    public WorkflowAnalysisResult Analyze(WorkflowDocument workflow, IWorkflowNodeDefinitionCatalog catalog);
}
