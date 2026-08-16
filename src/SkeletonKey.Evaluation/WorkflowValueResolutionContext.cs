using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;

namespace SkeletonKey.Evaluation;

/// <summary>
/// Represents immutable workflow data available to binding resolution and expression evaluation.
/// </summary>
/// <remarks>
/// Inputs and variables are defensively cloned. Names are ordinal and case-sensitive. Node outputs project zero values as missing,
/// one value as that JSON value including explicit null, and multiple values as an ordered JSON array.
/// The context contains no host services, resource instances, locator instances, clocks, randomness, or mutable runtime state.
/// </remarks>
public sealed class WorkflowValueResolutionContext
{
    private readonly IReadOnlyDictionary<string, JsonNode?> _inputs;
    private readonly IReadOnlyDictionary<string, JsonNode?> _variables;
    private readonly IReadOnlyDictionary<string, NodePortValueMap> _nodes;
    private readonly IReadOnlyDictionary<string, WorkflowIterationContext> _iterations;

    /// <summary>
    /// Initializes a new immutable value resolution context.
    /// </summary>
    /// <param name="inputs">Workflow inputs keyed by case-sensitive name.</param>
    /// <param name="variables">Workflow variables keyed by case-sensitive name.</param>
    /// <param name="nodes">Prior node output maps keyed by case-sensitive node ID.</param>
    /// <param name="iterations">Active iteration contexts keyed by case-sensitive loop node ID.</param>
    public WorkflowValueResolutionContext(
        IReadOnlyDictionary<string, JsonNode?>? inputs = null,
        IReadOnlyDictionary<string, JsonNode?>? variables = null,
        IReadOnlyDictionary<string, NodePortValueMap>? nodes = null,
        IReadOnlyDictionary<string, WorkflowIterationContext>? iterations = null)
    {
        _inputs = inputs is null ? EmptyJsonDictionary() : JsonClone.CloneDictionary(inputs);
        _variables = variables is null ? EmptyJsonDictionary() : JsonClone.CloneDictionary(variables);
        _nodes = nodes is null ? EmptyNodeDictionary() : CloneNodes(nodes);
        _iterations = iterations is null ? EmptyIterationDictionary() : CloneIterations(iterations);
    }

    /// <summary>
    /// Gets defensive copies of workflow inputs keyed by case-sensitive name.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> Inputs => JsonClone.CloneDictionary(_inputs);

    /// <summary>
    /// Gets defensive copies of workflow variables keyed by case-sensitive name.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> Variables => JsonClone.CloneDictionary(_variables);

    /// <summary>
    /// Gets defensive copies of prior node output maps keyed by case-sensitive node ID.
    /// </summary>
    public IReadOnlyDictionary<string, NodePortValueMap> Nodes => CloneNodes(_nodes);

    /// <summary>
    /// Gets a defensive collection copy of active iteration contexts keyed by case-sensitive loop node ID.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowIterationContext> Iterations => CloneIterations(_iterations);

    internal bool TryGetInput(string name, out JsonNode? value)
    {
        if (_inputs.TryGetValue(name, out JsonNode? stored))
        {
            value = stored?.DeepClone();
            return true;
        }

        value = null;
        return false;
    }

    internal bool TryGetVariable(string name, out JsonNode? value)
    {
        if (_variables.TryGetValue(name, out JsonNode? stored))
        {
            value = stored?.DeepClone();
            return true;
        }

        value = null;
        return false;
    }

    internal bool TryGetNode(string nodeId, out NodePortValueMap? node)
    {
        if (_nodes.TryGetValue(nodeId, out NodePortValueMap? stored))
        {
            node = new NodePortValueMap(stored.Values);
            return true;
        }

        node = null;
        return false;
    }

    internal bool TryGetIteration(string iterationId, out WorkflowIterationContext? iteration)
    {
        return _iterations.TryGetValue(iterationId, out iteration);
    }

    internal JsonObject ProjectInputs()
    {
        return JsonClone.CloneObject(_inputs);
    }

    internal JsonObject ProjectVariables()
    {
        return JsonClone.CloneObject(_variables);
    }

    internal JsonObject ProjectNodes()
    {
        JsonObject nodes = [];
        foreach (KeyValuePair<string, NodePortValueMap> node in _nodes)
        {
            JsonObject outputs = [];
            foreach (KeyValuePair<string, NodePortValueSet> port in node.Value.Values)
            {
                if (TryProjectPortValue(port.Value, out JsonNode? projected))
                {
                    outputs[port.Key] = projected;
                }
            }

            nodes[node.Key] = new JsonObject { ["outputs"] = outputs };
        }

        return nodes;
    }

    internal JsonObject ProjectIterations()
    {
        JsonObject iterations = [];
        foreach (KeyValuePair<string, WorkflowIterationContext> iteration in _iterations)
        {
            iterations[iteration.Key] = ProjectIteration(iteration.Value);
        }

        return iterations;
    }

    internal static bool TryProjectPortValue(NodePortValueSet values, out JsonNode? projected)
    {
        IReadOnlyList<JsonNode?> items = values.Values;
        if (items.Count == 0)
        {
            projected = null;
            return false;
        }

        if (items.Count == 1)
        {
            projected = items[0]?.DeepClone();
            return true;
        }

        JsonArray array = [];
        foreach (JsonNode? item in items)
        {
            array.Add(item?.DeepClone());
        }

        projected = array;
        return true;
    }

    internal static JsonObject ProjectIteration(WorkflowIterationContext iteration)
    {
        JsonObject value = new()
        {
            ["index"] = iteration.Index,
            ["number"] = iteration.Number,
        };

        if (iteration.HasItem)
        {
            value["item"] = iteration.Item;
        }

        if (iteration.Count is not null)
        {
            value["count"] = iteration.Count.Value;
        }

        return value;
    }

    private static IReadOnlyDictionary<string, JsonNode?> EmptyJsonDictionary()
    {
        return new ReadOnlyDictionary<string, JsonNode?>(new Dictionary<string, JsonNode?>(StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, NodePortValueMap> EmptyNodeDictionary()
    {
        return new ReadOnlyDictionary<string, NodePortValueMap>(new Dictionary<string, NodePortValueMap>(StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, WorkflowIterationContext> EmptyIterationDictionary()
    {
        return new ReadOnlyDictionary<string, WorkflowIterationContext>(new Dictionary<string, WorkflowIterationContext>(StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, NodePortValueMap> CloneNodes(IReadOnlyDictionary<string, NodePortValueMap> nodes)
    {
        Dictionary<string, NodePortValueMap> clone = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, NodePortValueMap> node in nodes)
        {
            clone[node.Key] = new NodePortValueMap(node.Value.Values);
        }

        return new ReadOnlyDictionary<string, NodePortValueMap>(clone);
    }

    private static IReadOnlyDictionary<string, WorkflowIterationContext> CloneIterations(IReadOnlyDictionary<string, WorkflowIterationContext> iterations)
    {
        Dictionary<string, WorkflowIterationContext> clone = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, WorkflowIterationContext> iteration in iterations)
        {
            clone[iteration.Key] = iteration.Value;
        }

        return new ReadOnlyDictionary<string, WorkflowIterationContext>(clone);
    }
}
