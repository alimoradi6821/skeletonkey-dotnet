using System.Collections.ObjectModel;
using SkeletonKey.Execution;

namespace SkeletonKey.Handlers;

/// <summary>
/// Represents immutable control and data outputs reported by a node handler.
/// </summary>
/// <remarks>
/// Control output IDs are ordered, ordinal, and case-sensitive. Data output values are defensively cloned through <see cref="NodePortValueSet" />.
/// Future runtime validation checks these outputs against the exact node definition.
/// </remarks>
public sealed class NodeHandlerOutputs
{
    private readonly IReadOnlyList<string> _activatedControlOutputs;
    private readonly NodePortValueMap _dataOutputs;

    /// <summary>
    /// Initializes a new handler output contract.
    /// </summary>
    /// <param name="activatedControlOutputs">The ordered control output ports activated by the handler.</param>
    /// <param name="dataOutputs">The ordered data output port values reported by the handler.</param>
    /// <exception cref="ArgumentException">Thrown when duplicate control output IDs are supplied.</exception>
    public NodeHandlerOutputs(
        IReadOnlyList<string>? activatedControlOutputs = null,
        IReadOnlyDictionary<string, NodePortValueSet>? dataOutputs = null)
    {
        _activatedControlOutputs = CopyDistinctControls(activatedControlOutputs);
        _dataOutputs = new NodePortValueMap(dataOutputs);
    }

    /// <summary>
    /// Gets a defensive copy of ordered control output ports activated by the handler.
    /// </summary>
    public IReadOnlyList<string> ActivatedControlOutputs => new ReadOnlyCollection<string>([.. _activatedControlOutputs]);

    /// <summary>
    /// Gets defensive copies of ordered data output port values reported by the handler.
    /// </summary>
    public IReadOnlyDictionary<string, NodePortValueSet> DataOutputs => _dataOutputs.Values;

    private static IReadOnlyList<string> CopyDistinctControls(IReadOnlyList<string>? controls)
    {
        if (controls is null)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        List<string> copy = new(controls.Count);
        foreach (string control in controls)
        {
            if (!seen.Add(control))
            {
                throw new ArgumentException("Activated control output IDs must be unique.", nameof(controls));
            }

            copy.Add(control);
        }

        return new ReadOnlyCollection<string>(copy);
    }
}
