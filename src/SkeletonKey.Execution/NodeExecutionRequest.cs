using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Execution;

/// <summary>
/// Represents the immutable request passed to a node handler for one exact node execution attempt.
/// </summary>
/// <remarks>
/// Parameters are expected to be fully materialized by a future runtime before normal handler invocation.
/// This contract does not evaluate bindings, expressions, resources, or locators. Parameter JSON and data-input JSON are defensively cloned.
/// </remarks>
public sealed class NodeExecutionRequest
{
    private readonly JsonObject _parameters;
    private readonly IReadOnlyList<string> _activatedControlInputs;
    private readonly NodePortValueMap _dataInputs;
    private readonly IReadOnlyDictionary<string, WorkflowIterationContext> _iterations;

    /// <summary>
    /// Initializes a new node execution request.
    /// </summary>
    /// <param name="identity">The exact node execution attempt identity.</param>
    /// <param name="parameters">The fully materialized handler parameters supplied by a future runtime.</param>
    /// <param name="activatedControlInputs">The ordered control input ports that activated the step.</param>
    /// <param name="dataInputs">The data-capable input port values connected to the step.</param>
    /// <param name="iterations">The active explicit iteration contexts keyed by loop node ID.</param>
    /// <exception cref="ArgumentException">Thrown when duplicate activated control input IDs are supplied.</exception>
    public NodeExecutionRequest(
        NodeExecutionIdentity identity,
        JsonObject? parameters = null,
        IReadOnlyList<string>? activatedControlInputs = null,
        IReadOnlyDictionary<string, NodePortValueSet>? dataInputs = null,
        IReadOnlyDictionary<string, WorkflowIterationContext>? iterations = null)
    {
        Identity = identity;
        _parameters = parameters is null ? [] : JsonClone.CloneObject(parameters);
        _activatedControlInputs = CopyDistinctControls(activatedControlInputs);
        _dataInputs = new NodePortValueMap(dataInputs);
        _iterations = iterations is null ? EmptyIterations() : CloneIterations(iterations);
    }

    /// <summary>
    /// Gets the exact node execution attempt identity.
    /// </summary>
    public NodeExecutionIdentity Identity { get; }

    /// <summary>
    /// Gets defensive copies of fully materialized handler parameters.
    /// </summary>
    public JsonObject Parameters => JsonClone.CloneObject(_parameters);

    /// <summary>
    /// Gets a defensive copy of ordered control input IDs that activated the step.
    /// </summary>
    public IReadOnlyList<string> ActivatedControlInputs => new ReadOnlyCollection<string>([.. _activatedControlInputs]);

    /// <summary>
    /// Gets defensive copies of data-capable input port values.
    /// </summary>
    public IReadOnlyDictionary<string, NodePortValueSet> DataInputs => _dataInputs.Values;

    /// <summary>
    /// Gets a defensive collection copy of active explicit iteration contexts keyed by loop node ID.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowIterationContext> Iterations => CloneIterations(_iterations);

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
                throw new ArgumentException("Activated control input IDs must be unique.", nameof(controls));
            }

            copy.Add(control);
        }

        return new ReadOnlyCollection<string>(copy);
    }

    private static IReadOnlyDictionary<string, WorkflowIterationContext> EmptyIterations()
    {
        return new ReadOnlyDictionary<string, WorkflowIterationContext>(new Dictionary<string, WorkflowIterationContext>(StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, WorkflowIterationContext> CloneIterations(IReadOnlyDictionary<string, WorkflowIterationContext> iterations)
    {
        Dictionary<string, WorkflowIterationContext> copy = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, WorkflowIterationContext> iteration in iterations)
        {
            copy[iteration.Key] = iteration.Value;
        }

        return new ReadOnlyDictionary<string, WorkflowIterationContext>(copy);
    }
}
