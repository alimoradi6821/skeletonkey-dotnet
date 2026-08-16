namespace SkeletonKey.Workflow.Designer;

/// <summary>
/// Represents the visual size of a workflow node in designer space.
/// </summary>
/// <param name="Width">The declared visual width.</param>
/// <param name="Height">The declared visual height.</param>
public readonly record struct WorkflowNodeSize(double Width, double Height);
