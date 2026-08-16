using System.Collections.ObjectModel;

namespace SkeletonKey.Execution;

/// <summary>
/// Represents an immutable ordered, case-sensitive map from node port IDs to JSON value sets.
/// </summary>
/// <remarks>
/// The dictionary preserves insertion order, uses ordinal case-sensitive port IDs, and defensively copies value sets.
/// Port multiplicity is enforced by future catalog-aware runtime validation.
/// </remarks>
public sealed class NodePortValueMap
{
    private readonly IReadOnlyDictionary<string, NodePortValueSet> _values;

    /// <summary>
    /// Initializes a new port value map.
    /// </summary>
    /// <param name="values">The ordered port values keyed by workflow port ID.</param>
    public NodePortValueMap(IReadOnlyDictionary<string, NodePortValueSet>? values = null)
    {
        _values = values is null ? EmptyDictionary() : CloneValues(values);
    }

    /// <summary>
    /// Gets defensive copies of port values keyed by case-sensitive workflow port ID.
    /// </summary>
    public IReadOnlyDictionary<string, NodePortValueSet> Values => CloneValues(_values);

    private static IReadOnlyDictionary<string, NodePortValueSet> EmptyDictionary()
    {
        return new ReadOnlyDictionary<string, NodePortValueSet>(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, NodePortValueSet> CloneValues(IReadOnlyDictionary<string, NodePortValueSet> values)
    {
        Dictionary<string, NodePortValueSet> clone = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, NodePortValueSet> value in values)
        {
            clone[value.Key] = new NodePortValueSet(value.Value.Values);
        }

        return new ReadOnlyDictionary<string, NodePortValueSet>(clone);
    }
}
