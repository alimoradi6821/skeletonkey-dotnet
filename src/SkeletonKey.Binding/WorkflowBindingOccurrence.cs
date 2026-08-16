using SkeletonKey.Workflow.Bindings;

namespace SkeletonKey.Binding;

/// <summary>
/// Represents one structured workflow binding occurrence discovered in a workflow value.
/// </summary>
/// <param name="Path">The JSON Pointer path to the binding wrapper.</param>
/// <param name="Binding">The parsed immutable binding declaration.</param>
public sealed record WorkflowBindingOccurrence(string Path, WorkflowBinding Binding);
