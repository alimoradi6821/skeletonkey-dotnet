using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Represents an immutable host-neutral final workflow execution result.
/// </summary>
/// <remarks>
/// Final outputs contain single and collection workflow outputs. Streamed records are emitted through events and
/// are not required to be duplicated in this dictionary. JSON output values are defensively cloned.
/// </remarks>
public sealed class WorkflowExecutionResult
{
    private static readonly IReadOnlyDictionary<string, JsonNode?> _emptyOutputs = new ReadOnlyDictionary<string, JsonNode?>(new Dictionary<string, JsonNode?>());
    private readonly IReadOnlyDictionary<string, JsonNode?> _outputs;

    /// <summary>
    /// Initializes a new workflow execution result.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="invocationId">The workflow invocation identifier. Root workflow results use their root invocation ID.</param>
    /// <param name="parentInvocationId">The optional parent workflow invocation identifier for child workflow results.</param>
    /// <param name="status">The final technical execution status.</param>
    /// <param name="outcome">Optional business outcome.</param>
    /// <param name="outputs">Optional final single and collection workflow outputs.</param>
    /// <param name="metrics">Final execution metrics.</param>
    /// <param name="error">Optional technical execution error.</param>
    public WorkflowExecutionResult(
        string executionId,
        string workflowId,
        string invocationId,
        string? parentInvocationId,
        WorkflowExecutionStatus status,
        WorkflowOutcome? outcome = null,
        IReadOnlyDictionary<string, JsonNode?>? outputs = null,
        WorkflowExecutionMetrics metrics = default,
        WorkflowError? error = null)
    {
        ExecutionId = executionId;
        WorkflowId = workflowId;
        InvocationId = invocationId;
        ParentInvocationId = parentInvocationId;
        Status = status;
        Outcome = outcome;
        _outputs = outputs is null ? _emptyOutputs : CloneOutputs(outputs);
        Metrics = metrics;
        Error = error;
    }

    /// <summary>
    /// Gets the execution identifier.
    /// </summary>
    public string ExecutionId { get; }

    /// <summary>
    /// Gets the workflow identifier.
    /// </summary>
    public string WorkflowId { get; }

    /// <summary>
    /// Gets the workflow invocation identifier.
    /// </summary>
    public string InvocationId { get; }

    /// <summary>
    /// Gets the optional parent workflow invocation identifier.
    /// </summary>
    public string? ParentInvocationId { get; }

    /// <summary>
    /// Gets the final technical execution status.
    /// </summary>
    public WorkflowExecutionStatus Status { get; }

    /// <summary>
    /// Gets the optional business outcome, distinct from technical execution status.
    /// </summary>
    public WorkflowOutcome? Outcome { get; }

    /// <summary>
    /// Gets defensive copies of final single and collection workflow outputs.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> Outputs => CloneOutputs(_outputs);

    /// <summary>
    /// Gets final execution metrics.
    /// </summary>
    public WorkflowExecutionMetrics Metrics { get; }

    /// <summary>
    /// Gets the optional technical execution error.
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
