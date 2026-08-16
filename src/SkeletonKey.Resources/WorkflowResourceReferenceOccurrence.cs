using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Resources;

/// <summary>
/// Describes a discovered resource reference wrapper and its JSON Pointer location.
/// </summary>
/// <param name="Path">The JSON Pointer path to the wrapper.</param>
/// <param name="Reference">The immutable resource reference.</param>
public sealed record WorkflowResourceReferenceOccurrence(string Path, WorkflowResourceReference Reference);
