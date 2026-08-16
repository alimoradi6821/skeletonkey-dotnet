using SkeletonKey.Expressions;

namespace SkeletonKey.Evaluation;

/// <summary>
/// Defines deterministic, side-effect-free workflow expression evaluation.
/// </summary>
public interface IWorkflowExpressionEvaluator
{
    /// <summary>
    /// Evaluates a parsed workflow expression document.
    /// </summary>
    /// <param name="expression">The expression document produced by the existing parser.</param>
    /// <param name="context">The immutable value resolution context.</param>
    /// <param name="limits">Optional deterministic processing limits.</param>
    /// <returns>The expression value or a structured evaluation error.</returns>
    public WorkflowValueResult Evaluate(
        WorkflowExpressionDocument expression,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits? limits = null);

    /// <summary>
    /// Parses and evaluates expression text using the existing parser for diagnostics.
    /// </summary>
    /// <param name="expression">The exact expression text.</param>
    /// <param name="context">The immutable value resolution context.</param>
    /// <param name="limits">Optional deterministic processing limits.</param>
    /// <returns>The expression value or a structured parse/evaluation error.</returns>
    public WorkflowValueResult Evaluate(
        string expression,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits? limits = null);
}
