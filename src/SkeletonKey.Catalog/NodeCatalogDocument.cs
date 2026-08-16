using System.Collections.ObjectModel;

namespace SkeletonKey.Catalog;

/// <summary>
/// Represents an immutable node catalog artifact.
/// </summary>
public sealed class NodeCatalogDocument
{
    private static readonly IReadOnlyList<WorkflowNodeDefinition> _emptyDefinitions = Array.AsReadOnly(Array.Empty<WorkflowNodeDefinition>());

    /// <summary>
    /// Initializes a node catalog document.
    /// </summary>
    /// <param name="schema">The node catalog schema URI declaration.</param>
    /// <param name="specVersion">The node catalog specification version.</param>
    /// <param name="id">The catalog identifier.</param>
    /// <param name="version">The exact catalog artifact version.</param>
    /// <param name="name">Optional human-readable catalog name.</param>
    /// <param name="description">Optional human-readable catalog description.</param>
    /// <param name="definitions">Catalog node definitions in stable declaration order.</param>
    public NodeCatalogDocument(
        string schema = NodeCatalogSpecification.CurrentSchemaUri,
        string specVersion = NodeCatalogSpecification.CurrentVersion,
        string id = "",
        string version = "",
        string? name = null,
        string? description = null,
        IReadOnlyList<WorkflowNodeDefinition>? definitions = null)
    {
        Schema = schema;
        SpecVersion = specVersion;
        Id = id;
        Version = version;
        Name = name;
        Description = description;
        Definitions = definitions is null ? _emptyDefinitions : new ReadOnlyCollection<WorkflowNodeDefinition>([.. definitions]);
    }

    /// <summary>
    /// Gets the node catalog schema URI declaration.
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// Gets the node catalog specification version.
    /// </summary>
    public string SpecVersion { get; }

    /// <summary>
    /// Gets the catalog identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the exact catalog artifact version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets an optional human-readable catalog name.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets an optional human-readable catalog description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets catalog node definitions in stable declaration order.
    /// </summary>
    public IReadOnlyList<WorkflowNodeDefinition> Definitions { get; }
}
