namespace SkeletonKey.Catalog;

/// <summary>
/// Identifies one versioned workflow node definition in a catalog.
/// </summary>
/// <param name="Type">The namespace-style node type identifier.</param>
/// <param name="Version">The node type version.</param>
public readonly record struct WorkflowNodeDefinitionKey(string Type, int Version);
