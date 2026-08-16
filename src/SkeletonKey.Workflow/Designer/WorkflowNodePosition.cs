namespace SkeletonKey.Workflow.Designer;

/// <summary>
/// Represents the visual position of a workflow node in designer space.
/// </summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public readonly record struct WorkflowNodePosition(double X, double Y);
