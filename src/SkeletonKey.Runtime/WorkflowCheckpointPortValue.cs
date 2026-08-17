using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SkeletonKey.Runtime;

/// <summary>Represents the ordered persisted values of one node output port.</summary>
public sealed class WorkflowCheckpointPortValue
{
    private readonly IReadOnlyList<JsonNode?> _values;

    /// <summary>Initializes a persisted port value.</summary>
    public WorkflowCheckpointPortValue(string portId, IReadOnlyList<JsonNode?>? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portId);
        PortId = portId;
        _values = Array.AsReadOnly([.. (values ?? Array.AsReadOnly(Array.Empty<JsonNode?>())).Select(static value => value?.DeepClone())]);
    }

    /// <summary>Gets the case-sensitive output port identifier.</summary>
    public string PortId { get; }

    /// <summary>Gets defensive copies of ordered JSON values.</summary>
    public IReadOnlyList<JsonNode?> Values => new ReadOnlyCollection<JsonNode?>([.. _values.Select(static value => value?.DeepClone())]);
}
