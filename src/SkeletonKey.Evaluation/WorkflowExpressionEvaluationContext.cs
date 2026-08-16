namespace SkeletonKey.Evaluation;

/// <summary>
/// Represents immutable data and deterministic limits for one expression evaluation.
/// </summary>
/// <remarks>
/// The context exposes no host services, resources, locators, clocks, randomness, filesystem, network, or mutable runtime state.
/// </remarks>
public sealed class WorkflowExpressionEvaluationContext
{
    /// <summary>
    /// Initializes a new expression evaluation context.
    /// </summary>
    /// <param name="values">The immutable workflow value resolution context.</param>
    /// <param name="limits">Optional deterministic evaluation limits.</param>
    public WorkflowExpressionEvaluationContext(
        WorkflowValueResolutionContext values,
        WorkflowValueProcessingLimits? limits = null)
    {
        Values = values;
        Limits = limits ?? WorkflowValueProcessingLimits.Default;
    }

    /// <summary>
    /// Gets the immutable workflow value resolution context.
    /// </summary>
    public WorkflowValueResolutionContext Values { get; }

    /// <summary>
    /// Gets deterministic evaluation limits.
    /// </summary>
    public WorkflowValueProcessingLimits Limits { get; }
}
