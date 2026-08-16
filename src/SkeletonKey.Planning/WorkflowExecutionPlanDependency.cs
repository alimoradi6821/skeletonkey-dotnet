namespace SkeletonKey.Planning;

/// <summary>
/// Describes a dependency from one planned step to another.
/// </summary>
public sealed class WorkflowExecutionPlanDependency
{
    /// <summary>
    /// Initializes a planned step dependency.
    /// </summary>
    /// <param name="stepId">The predecessor step identifier.</param>
    /// <param name="kind">The dependency kind.</param>
    /// <param name="sourcePort">Optional source port name.</param>
    /// <param name="targetPort">Optional target port name.</param>
    /// <param name="targetStepId">Optional dependent step identifier for plan-wide dependency views.</param>
    /// <param name="sourcePath">Optional JSON Pointer to the workflow value that declared the dependency.</param>
    public WorkflowExecutionPlanDependency(
        string stepId,
        WorkflowExecutionPlanDependencyKind kind = WorkflowExecutionPlanDependencyKind.Control,
        string? sourcePort = null,
        string? targetPort = null,
        string? targetStepId = null,
        string? sourcePath = null)
    {
        StepId = stepId;
        Kind = kind;
        SourcePort = sourcePort;
        TargetPort = targetPort;
        TargetStepId = targetStepId;
        SourcePath = sourcePath;
    }

    /// <summary>
    /// Gets the predecessor step identifier.
    /// </summary>
    public string StepId { get; }

    /// <summary>
    /// Gets the dependency kind.
    /// </summary>
    public WorkflowExecutionPlanDependencyKind Kind { get; }

    /// <summary>
    /// Gets an optional source port name.
    /// </summary>
    public string? SourcePort { get; }

    /// <summary>
    /// Gets an optional target port name.
    /// </summary>
    public string? TargetPort { get; }

    /// <summary>
    /// Gets the optional dependent step identifier for plan-wide dependency views.
    /// </summary>
    public string? TargetStepId { get; }

    /// <summary>
    /// Gets the optional JSON Pointer to the workflow value that declared the dependency.
    /// </summary>
    public string? SourcePath { get; }
}
