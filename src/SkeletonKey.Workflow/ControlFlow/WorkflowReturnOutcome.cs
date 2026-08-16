using System.Text.Json.Nodes;

namespace SkeletonKey.Workflow.ControlFlow;

/// <summary>
/// Represents an immutable early-return outcome declaration stored as workflow data.
/// </summary>
/// <remarks>
/// The contract describes deferred runtime behavior only. Message and data values may contain workflow
/// values such as bindings or expressions and are defensively cloned.
/// </remarks>
public sealed class WorkflowReturnOutcome
{
    private readonly JsonNode? _message;
    private readonly JsonNode? _data;

    /// <summary>
    /// Initializes a new early-return outcome declaration.
    /// </summary>
    /// <param name="kind">The declared outcome kind.</param>
    /// <param name="code">The stable outcome code.</param>
    /// <param name="message">Optional message workflow value.</param>
    /// <param name="data">Optional data workflow value.</param>
    public WorkflowReturnOutcome(
        string kind,
        string code,
        JsonNode? message = null,
        JsonNode? data = null)
    {
        Kind = kind;
        Code = code;
        _message = message?.DeepClone();
        _data = data?.DeepClone();
    }

    /// <summary>
    /// Gets the declared outcome kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the stable outcome code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets a defensive copy of the optional message workflow value.
    /// </summary>
    public JsonNode? Message => _message?.DeepClone();

    /// <summary>
    /// Gets a defensive copy of optional outcome data workflow value.
    /// </summary>
    public JsonNode? Data => _data?.DeepClone();
}
