using System.Text.Json.Nodes;
using SkeletonKey.Evaluation;

namespace SkeletonKey.Materialization;

/// <summary>
/// Defines recursive materialization of workflow-value JSON into handler-ready JSON.
/// </summary>
public interface IWorkflowValueMaterializer
{
    /// <summary>
    /// Materializes workflow-value JSON by resolving bindings, evaluating expressions, and unwrapping literal wrappers.
    /// </summary>
    /// <param name="workflowValue">The workflow-value JSON to materialize.</param>
    /// <param name="context">The immutable value resolution context.</param>
    /// <param name="limits">Optional deterministic processing limits.</param>
    /// <param name="jsonPath">The workflow JSON path associated with diagnostics.</param>
    /// <returns>The materialized JSON value or a structured error.</returns>
    public WorkflowValueResult Materialize(
        JsonNode? workflowValue,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits? limits = null,
        string jsonPath = "");
}
