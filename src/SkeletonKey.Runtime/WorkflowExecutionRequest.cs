using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Runtime;

/// <summary>
/// Represents an immutable workflow runtime request.
/// </summary>
/// <remarks>
/// The workflow document is not mutated. Input and variable JSON values are defensively cloned. Execution and plan IDs are supplied by the caller;
/// the runtime does not generate random or time-based identities. A missing event sink is replaced by an explicit no-op sink.
/// </remarks>
public sealed class WorkflowExecutionRequest
{
    private readonly IReadOnlyDictionary<string, JsonNode?> _inputs;
    private readonly IReadOnlyDictionary<string, JsonNode?> _variables;

    /// <summary>
    /// Initializes a new workflow runtime request.
    /// </summary>
    /// <param name="workflow">The immutable workflow document to execute.</param>
    /// <param name="executionId">The caller-supplied root execution identifier.</param>
    /// <param name="planId">The caller-supplied execution plan identifier used for runtime state and node identity.</param>
    /// <param name="inputs">Optional workflow input values.</param>
    /// <param name="variables">Optional workflow variable overrides or initial values.</param>
    /// <param name="eventSink">Optional event sink; when omitted, runtime events are accepted by a no-op sink.</param>
    /// <param name="checkpointStore">Optional host-owned durable checkpoint store.</param>
    /// <param name="resumeCheckpoint">Optional previously loaded checkpoint to resume.</param>
    public WorkflowExecutionRequest(
        WorkflowDocument workflow,
        string executionId,
        string planId,
        IReadOnlyDictionary<string, JsonNode?>? inputs = null,
        IReadOnlyDictionary<string, JsonNode?>? variables = null,
        IWorkflowEventSink? eventSink = null,
        IWorkflowCheckpointStore? checkpointStore = null,
        WorkflowExecutionCheckpoint? resumeCheckpoint = null)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);

        Workflow = workflow;
        ExecutionId = executionId;
        PlanId = planId;
        _inputs = CloneJsonDictionary(inputs);
        _variables = CloneJsonDictionary(variables);
        EventSink = eventSink ?? NoOpWorkflowEventSink.Instance;
        CheckpointStore = checkpointStore;
        ResumeCheckpoint = resumeCheckpoint;
    }

    /// <summary>
    /// Gets the immutable workflow document to execute.
    /// </summary>
    public WorkflowDocument Workflow { get; }

    /// <summary>
    /// Gets the caller-supplied root execution identifier.
    /// </summary>
    public string ExecutionId { get; }

    /// <summary>
    /// Gets the caller-supplied execution plan identifier used for runtime state and node identity.
    /// </summary>
    public string PlanId { get; }

    /// <summary>
    /// Gets defensive copies of workflow input values.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> Inputs => CloneJsonDictionary(_inputs);

    /// <summary>
    /// Gets defensive copies of workflow variable values.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> Variables => CloneJsonDictionary(_variables);

    /// <summary>
    /// Gets the event sink that receives runtime-owned ordered workflow events.
    /// </summary>
    public IWorkflowEventSink EventSink { get; }

    /// <summary>Gets the optional host-owned durable checkpoint store.</summary>
    public IWorkflowCheckpointStore? CheckpointStore { get; }

    /// <summary>Gets the optional checkpoint from which execution resumes.</summary>
    public WorkflowExecutionCheckpoint? ResumeCheckpoint { get; }

    private static IReadOnlyDictionary<string, JsonNode?> CloneJsonDictionary(IReadOnlyDictionary<string, JsonNode?>? values)
    {
        Dictionary<string, JsonNode?> clone = new(StringComparer.Ordinal);
        if (values is not null)
        {
            foreach (KeyValuePair<string, JsonNode?> value in values)
            {
                clone[value.Key] = value.Value?.DeepClone();
            }
        }

        return new ReadOnlyDictionary<string, JsonNode?>(clone);
    }

    private sealed class NoOpWorkflowEventSink : IWorkflowEventSink
    {
        public static readonly NoOpWorkflowEventSink Instance = new();

        public ValueTask PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
