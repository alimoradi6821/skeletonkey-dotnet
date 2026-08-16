using System.Text.Json.Nodes;

namespace SkeletonKey.Workflow.Bindings;

/// <summary>
/// Represents an immutable structured workflow data binding declaration.
/// </summary>
/// <remarks>
/// Binding paths use read-only RFC 6901 JSON Pointer syntax. Missing-value behavior is declared but not
/// executed in this phase. Default JSON values are defensively cloned.
/// </remarks>
public sealed class WorkflowBinding
{
    private readonly JsonNode? _default;

    /// <summary>
    /// Initializes a new workflow binding declaration.
    /// </summary>
    /// <param name="source">The local binding source kind.</param>
    /// <param name="name">The input or variable name for input and variable bindings.</param>
    /// <param name="node">The node identifier for node output bindings.</param>
    /// <param name="port">The node output port for node output bindings.</param>
    /// <param name="iteration">The explicit iteration identifier for iteration bindings.</param>
    /// <param name="path">The read-only RFC 6901 JSON Pointer path.</param>
    /// <param name="onMissing">The missing-value behavior.</param>
    /// <param name="defaultValue">The optional default JSON value.</param>
    /// <param name="hasDefault">Whether the default property was explicitly declared, including explicit JSON null.</param>
    public WorkflowBinding(
        WorkflowBindingSource source,
        string? name = null,
        string? node = null,
        string? port = null,
        string? iteration = null,
        string path = "",
        WorkflowBindingMissingBehavior onMissing = WorkflowBindingMissingBehavior.Error,
        JsonNode? defaultValue = null,
        bool hasDefault = false)
    {
        Source = source;
        Name = name;
        Node = node;
        Port = port;
        Iteration = iteration;
        Path = path;
        OnMissing = onMissing;
        _default = defaultValue?.DeepClone();
        HasDefault = hasDefault;
    }

    /// <summary>
    /// Gets the local binding source kind.
    /// </summary>
    public WorkflowBindingSource Source { get; }

    /// <summary>
    /// Gets the input or variable name for input and variable bindings.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the node identifier for node output bindings.
    /// </summary>
    public string? Node { get; }

    /// <summary>
    /// Gets the node output port for node output bindings.
    /// </summary>
    public string? Port { get; }

    /// <summary>
    /// Gets the explicit iteration identifier for iteration bindings.
    /// </summary>
    public string? Iteration { get; }

    /// <summary>
    /// Gets the read-only RFC 6901 JSON Pointer path. The empty string selects the whole source value.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the declared missing-value behavior.
    /// </summary>
    public WorkflowBindingMissingBehavior OnMissing { get; }

    /// <summary>
    /// Gets a value indicating whether a default value was explicitly declared.
    /// </summary>
    public bool HasDefault { get; }

    /// <summary>
    /// Gets a defensive copy of the optional default JSON value.
    /// </summary>
    public JsonNode? Default => _default?.DeepClone();
}
