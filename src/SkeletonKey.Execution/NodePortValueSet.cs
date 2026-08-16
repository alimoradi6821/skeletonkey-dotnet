using System.Text.Json.Nodes;

namespace SkeletonKey.Execution;

/// <summary>
/// Represents the immutable ordered JSON values supplied through one node port.
/// </summary>
/// <remarks>
/// An empty set means no value was supplied. A set containing one <see langword="null" /> item represents one explicit JSON null value.
/// Values are defensively cloned on input and output.
/// </remarks>
public sealed class NodePortValueSet
{
    private readonly IReadOnlyList<JsonNode?> _values;

    /// <summary>
    /// Initializes a new port value set.
    /// </summary>
    /// <param name="values">The ordered JSON values for the port. Items may be <see langword="null" /> to preserve explicit JSON null.</param>
    public NodePortValueSet(IEnumerable<JsonNode?>? values = null)
    {
        _values = values is null ? Array.AsReadOnly(Array.Empty<JsonNode?>()) : JsonClone.CloneNodes(values);
    }

    /// <summary>
    /// Gets defensive copies of the ordered JSON values for the port.
    /// </summary>
    public IReadOnlyList<JsonNode?> Values => JsonClone.CloneNodes(_values);
}
