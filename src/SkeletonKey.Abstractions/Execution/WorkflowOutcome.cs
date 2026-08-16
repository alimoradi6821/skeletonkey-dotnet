using System.Text.Json.Nodes;

namespace SkeletonKey.Abstractions.Execution;

/// <summary>
/// Represents an immutable host-neutral business outcome for a workflow execution.
/// </summary>
/// <remarks>
/// Business outcomes are distinct from technical execution status. JSON data is defensively cloned on input
/// and output so callers cannot mutate internal state.
/// </remarks>
public sealed class WorkflowOutcome
{
    private readonly JsonObject? _data;

    /// <summary>
    /// Initializes a new workflow outcome.
    /// </summary>
    /// <param name="kind">The business outcome kind.</param>
    /// <param name="code">The stable business outcome code.</param>
    /// <param name="message">Optional human-readable outcome message.</param>
    /// <param name="data">Optional JSON outcome data.</param>
    public WorkflowOutcome(
        WorkflowOutcomeKind kind,
        string code,
        string? message = null,
        JsonObject? data = null)
    {
        Kind = kind;
        Code = code;
        Message = message;
        _data = data is null ? null : (JsonObject)data.DeepClone();
    }

    /// <summary>
    /// Gets the business outcome kind.
    /// </summary>
    public WorkflowOutcomeKind Kind { get; }

    /// <summary>
    /// Gets the stable business outcome code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets optional human-readable outcome message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets a defensive copy of optional JSON outcome data.
    /// </summary>
    public JsonObject? Data => _data is null ? null : (JsonObject)_data.DeepClone();
}
