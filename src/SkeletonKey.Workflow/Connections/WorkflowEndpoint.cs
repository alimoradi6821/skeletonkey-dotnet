namespace SkeletonKey.Workflow.Connections;

/// <summary>
/// Identifies one named port on one workflow node.
/// </summary>
/// <param name="Node">The target node identifier.</param>
/// <param name="Port">The target port name.</param>
public readonly record struct WorkflowEndpoint(string Node, string Port);
