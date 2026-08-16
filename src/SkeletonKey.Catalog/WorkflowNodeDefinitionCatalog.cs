using System.Collections.ObjectModel;

namespace SkeletonKey.Catalog;

/// <summary>
/// Provides an immutable in-memory workflow node definition catalog.
/// </summary>
public sealed class WorkflowNodeDefinitionCatalog : IWorkflowNodeDefinitionCatalog
{
    private static readonly IReadOnlyList<WorkflowNodeDefinition> _emptyDefinitions = Array.AsReadOnly(Array.Empty<WorkflowNodeDefinition>());
    private readonly IReadOnlyDictionary<WorkflowNodeDefinitionKey, WorkflowNodeDefinition> _byKey;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<WorkflowNodeDefinition>> _byType;

    /// <summary>
    /// Initializes an immutable catalog from the supplied definitions.
    /// </summary>
    /// <param name="definitions">Definitions exposed by the catalog.</param>
    /// <exception cref="ArgumentException">Thrown when two definitions share the same type and version.</exception>
    public WorkflowNodeDefinitionCatalog(IReadOnlyList<WorkflowNodeDefinition>? definitions = null)
    {
        if (definitions is null || definitions.Count == 0)
        {
            Definitions = _emptyDefinitions;
            _byKey = new ReadOnlyDictionary<WorkflowNodeDefinitionKey, WorkflowNodeDefinition>(new Dictionary<WorkflowNodeDefinitionKey, WorkflowNodeDefinition>());
            _byType = new ReadOnlyDictionary<string, IReadOnlyList<WorkflowNodeDefinition>>(new Dictionary<string, IReadOnlyList<WorkflowNodeDefinition>>(StringComparer.Ordinal));
            return;
        }

        Dictionary<WorkflowNodeDefinitionKey, WorkflowNodeDefinition> byKey = new();
        Dictionary<string, List<WorkflowNodeDefinition>> byType = new(StringComparer.Ordinal);

        foreach (WorkflowNodeDefinition definition in definitions)
        {
            if (!byKey.TryAdd(definition.Key, definition))
            {
                throw new ArgumentException("Duplicate workflow node definition type and version.", nameof(definitions));
            }

            if (!byType.TryGetValue(definition.Type, out List<WorkflowNodeDefinition>? typedDefinitions))
            {
                typedDefinitions = [];
                byType.Add(definition.Type, typedDefinitions);
            }

            typedDefinitions.Add(definition);
        }

        foreach (List<WorkflowNodeDefinition> typedDefinitions in byType.Values)
        {
            typedDefinitions.Sort(static (left, right) => left.Version.CompareTo(right.Version));
        }

        Definitions = Array.AsReadOnly([.. definitions]);
        _byKey = new ReadOnlyDictionary<WorkflowNodeDefinitionKey, WorkflowNodeDefinition>(byKey);
        _byType = new ReadOnlyDictionary<string, IReadOnlyList<WorkflowNodeDefinition>>(
            byType.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<WorkflowNodeDefinition>)Array.AsReadOnly([.. pair.Value]),
                StringComparer.Ordinal));
    }

    /// <inheritdoc />
    public IReadOnlyList<WorkflowNodeDefinition> Definitions { get; }

    /// <inheritdoc />
    public bool TryGetDefinition(string type, int version, out WorkflowNodeDefinition? definition)
    {
        return _byKey.TryGetValue(new WorkflowNodeDefinitionKey(type, version), out definition);
    }

    /// <inheritdoc />
    public IReadOnlyList<WorkflowNodeDefinition> GetDefinitions(string type)
    {
        return _byType.TryGetValue(type, out IReadOnlyList<WorkflowNodeDefinition>? definitions) ? definitions : _emptyDefinitions;
    }
}
