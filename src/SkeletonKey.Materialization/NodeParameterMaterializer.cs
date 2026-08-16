using System.Text.Json.Nodes;
using SkeletonKey.Evaluation;

namespace SkeletonKey.Materialization;

/// <summary>
/// Materializes node parameter objects into plain JSON for future node execution requests.
/// </summary>
/// <remarks>
/// The helper is stateless, deterministic, thread-safe, and does not construct or execute <c>NodeExecutionRequest</c> instances.
/// Resource and locator wrappers fail explicitly unless a future specialized preparation phase handles them separately.
/// </remarks>
public sealed class NodeParameterMaterializer
{
    private readonly IWorkflowValueMaterializer _materializer;

    /// <summary>
    /// Initializes a new node parameter materializer.
    /// </summary>
    /// <param name="materializer">Optional workflow value materializer.</param>
    public NodeParameterMaterializer(IWorkflowValueMaterializer? materializer = null)
    {
        _materializer = materializer ?? new WorkflowValueMaterializer();
    }

    /// <summary>
    /// Materializes a node parameter object into plain JSON.
    /// </summary>
    /// <param name="parameters">The source node parameter object.</param>
    /// <param name="context">The immutable value resolution context.</param>
    /// <param name="limits">Optional deterministic processing limits.</param>
    /// <returns>A successful JSON object result or a structured materialization error.</returns>
    public WorkflowValueResult MaterializeParameters(
        JsonObject parameters,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits? limits = null)
    {
        WorkflowValueResult result = _materializer.Materialize(parameters, context, limits);
        if (!result.IsSuccess)
        {
            return result;
        }

        return result.Value is JsonObject
            ? result
            : WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.MalformedWorkflowValueWrapper, "Materialized parameters must be a JSON object.", string.Empty));
    }
}
