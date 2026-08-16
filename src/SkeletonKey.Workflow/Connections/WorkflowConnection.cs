namespace SkeletonKey.Workflow.Connections;

/// <summary>
/// Represents a directed connection between two workflow endpoints.
/// </summary>
/// <param name="From">The source endpoint.</param>
/// <param name="To">The target endpoint.</param>
public readonly record struct WorkflowConnection(WorkflowEndpoint From, WorkflowEndpoint To);
