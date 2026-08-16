namespace SkeletonKey.Catalog;

/// <summary>
/// Describes a deterministic source of versioned workflow node definitions.
/// </summary>
public interface IWorkflowNodeDefinitionCatalog
{
    /// <summary>
    /// Gets all definitions exposed by the catalog.
    /// </summary>
    public IReadOnlyList<WorkflowNodeDefinition> Definitions { get; }

    /// <summary>
    /// Attempts to retrieve the exact node definition for the supplied type and version.
    /// </summary>
    /// <param name="type">The node type identifier.</param>
    /// <param name="version">The node type version.</param>
    /// <param name="definition">The exact matching definition, when available.</param>
    /// <returns><see langword="true" /> when the exact definition is available.</returns>
    public bool TryGetDefinition(string type, int version, out WorkflowNodeDefinition? definition);

    /// <summary>
    /// Gets all known versions for the supplied node type.
    /// </summary>
    /// <param name="type">The node type identifier.</param>
    /// <returns>Known definitions for the supplied type in catalog enumeration order.</returns>
    public IReadOnlyList<WorkflowNodeDefinition> GetDefinitions(string type);
}
