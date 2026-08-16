using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Represents an immutable host-neutral node execution result.
/// </summary>
/// <remarks>
/// Output dictionary keys represent node output port names. JSON output values are defensively cloned.
/// </remarks>
public sealed class NodeExecutionResult
{
    private static readonly IReadOnlyDictionary<string, JsonNode?> _emptyOutputs = new ReadOnlyDictionary<string, JsonNode?>(new Dictionary<string, JsonNode?>());
    private readonly IReadOnlyDictionary<string, JsonNode?> _outputs;

    /// <summary>
    /// Initializes a new node execution result.
    /// </summary>
    /// <param name="executionId">The root workflow execution identifier.</param>
    /// <param name="workflowId">The workflow identifier containing the node.</param>
    /// <param name="invocationId">The workflow invocation identifier containing the node.</param>
    /// <param name="nodeId">The node identifier.</param>
    /// <param name="nodeType">The node type identifier.</param>
    /// <param name="status">The node execution status.</param>
    /// <param name="attempt">The execution attempt number.</param>
    /// <param name="outputs">Optional node output port values.</param>
    /// <param name="error">Optional technical node error.</param>
    public NodeExecutionResult(
        string executionId,
        string workflowId,
        string invocationId,
        string nodeId,
        string nodeType,
        NodeExecutionStatus status,
        int attempt,
        IReadOnlyDictionary<string, JsonNode?>? outputs = null,
        WorkflowError? error = null)
    {
        ExecutionId = executionId;
        WorkflowId = workflowId;
        InvocationId = invocationId;
        NodeId = nodeId;
        NodeType = nodeType;
        Status = status;
        Attempt = attempt;
        _outputs = outputs is null ? _emptyOutputs : CloneOutputs(outputs);
        Error = error;
    }

    /// <summary>
    /// Gets the root workflow execution identifier.
    /// </summary>
    public string ExecutionId { get; }

    /// <summary>
    /// Gets the workflow identifier containing the node.
    /// </summary>
    public string WorkflowId { get; }

    /// <summary>
    /// Gets the workflow invocation identifier containing the node.
    /// </summary>
    public string InvocationId { get; }

    /// <summary>
    /// Gets the node identifier.
    /// </summary>
    public string NodeId { get; }

    /// <summary>
    /// Gets the node type identifier.
    /// </summary>
    public string NodeType { get; }

    /// <summary>
    /// Gets the node execution status.
    /// </summary>
    public NodeExecutionStatus Status { get; }

    /// <summary>
    /// Gets the execution attempt number.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Gets defensive copies of node output port values.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> Outputs => CloneOutputs(_outputs);

    /// <summary>
    /// Gets the optional technical node error.
    /// </summary>
    public WorkflowError? Error { get; }

    private static IReadOnlyDictionary<string, JsonNode?> CloneOutputs(IReadOnlyDictionary<string, JsonNode?> outputs)
    {
        Dictionary<string, JsonNode?> clone = new(outputs.Count);

        foreach (KeyValuePair<string, JsonNode?> output in outputs)
        {
            clone[output.Key] = output.Value?.DeepClone();
        }

        return new ReadOnlyDictionary<string, JsonNode?>(clone);
    }
}
