using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Interaction;

/// <summary>
/// Describes an immutable materialized human interaction request for a future host runtime.
/// </summary>
/// <remarks>
/// Prompts are materialized before construction. Secret requests are sensitive, prohibit defaults by
/// convention, and future hosts are responsible for redacting secret values from logs and traces.
/// </remarks>
public sealed class WorkflowInteractionRequest
{
    private static readonly IReadOnlyList<WorkflowInteractionOption> _emptyOptions = Array.AsReadOnly(Array.Empty<WorkflowInteractionOption>());
    private readonly JsonNode? _default;
    private readonly JsonObject? _metadata;

    /// <summary>
    /// Initializes a host-neutral interaction request.
    /// </summary>
    /// <param name="requestId">The runtime-supplied request identifier.</param>
    /// <param name="executionId">The execution identity.</param>
    /// <param name="invocationId">The workflow invocation identity.</param>
    /// <param name="workflowId">The workflow document identity.</param>
    /// <param name="nodeId">The requesting node identity.</param>
    /// <param name="kind">The requested interaction kind.</param>
    /// <param name="prompt">The already materialized prompt text.</param>
    /// <param name="description">Optional already materialized descriptive text.</param>
    /// <param name="options">Ordered static options for choice interactions.</param>
    /// <param name="required">Whether a submitted value is required.</param>
    /// <param name="hasDefault">Whether <paramref name="defaultValue" /> was explicitly supplied, including JSON null.</param>
    /// <param name="defaultValue">Optional default JSON value cloned defensively.</param>
    /// <param name="timeout">Optional timeout for future host waiting behavior.</param>
    /// <param name="metadata">Optional host-neutral metadata cloned defensively.</param>
    public WorkflowInteractionRequest(
        string requestId,
        string executionId,
        string invocationId,
        string workflowId,
        string nodeId,
        WorkflowInteractionKind kind,
        string prompt,
        string? description = null,
        IReadOnlyList<WorkflowInteractionOption>? options = null,
        bool required = true,
        bool hasDefault = false,
        JsonNode? defaultValue = null,
        TimeSpan? timeout = null,
        JsonObject? metadata = null)
    {
        RequestId = requestId;
        ExecutionId = executionId;
        InvocationId = invocationId;
        WorkflowId = workflowId;
        NodeId = nodeId;
        Kind = kind;
        Prompt = prompt;
        Description = description;
        Options = options is null ? _emptyOptions : new ReadOnlyCollection<WorkflowInteractionOption>([.. options]);
        Required = required;
        HasDefault = hasDefault;
        _default = hasDefault ? defaultValue?.DeepClone() : null;
        Timeout = timeout;
        _metadata = metadata is null ? null : (JsonObject)metadata.DeepClone();
    }

    /// <summary>Gets the runtime-supplied request identifier.</summary>
    public string RequestId { get; }

    /// <summary>Gets the execution identity.</summary>
    public string ExecutionId { get; }

    /// <summary>Gets the workflow invocation identity.</summary>
    public string InvocationId { get; }

    /// <summary>Gets the workflow document identity.</summary>
    public string WorkflowId { get; }

    /// <summary>Gets the requesting node identity.</summary>
    public string NodeId { get; }

    /// <summary>Gets the requested interaction kind.</summary>
    public WorkflowInteractionKind Kind { get; }

    /// <summary>Gets the already materialized prompt text.</summary>
    public string Prompt { get; }

    /// <summary>Gets optional already materialized descriptive text.</summary>
    public string? Description { get; }

    /// <summary>Gets ordered immutable choice options.</summary>
    public IReadOnlyList<WorkflowInteractionOption> Options { get; }

    /// <summary>Gets a value indicating whether a submitted value is required.</summary>
    public bool Required { get; }

    /// <summary>Gets a value indicating whether a default was explicitly supplied.</summary>
    public bool HasDefault { get; }

    /// <summary>Gets a defensive clone of the optional default value, including explicit JSON null.</summary>
    public JsonNode? Default => _default?.DeepClone();

    /// <summary>Gets the optional timeout used by future host waiting behavior.</summary>
    public TimeSpan? Timeout { get; }

    /// <summary>Gets a defensive clone of optional provider-neutral metadata.</summary>
    public JsonObject? Metadata => _metadata is null ? null : (JsonObject)_metadata.DeepClone();
}
