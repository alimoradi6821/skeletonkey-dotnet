using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Policies;

namespace SkeletonKey.Workflow.Nodes;

/// <summary>
/// Represents a node instance declared inside a workflow document.
/// </summary>
public sealed class WorkflowNode
{
    private readonly JsonObject _parameters;

    /// <summary>
    /// Initializes a new workflow node declaration.
    /// </summary>
    /// <param name="id">The node identifier within the workflow document.</param>
    /// <param name="type">The namespace-style node type identifier.</param>
    /// <param name="typeVersion">The declared node type version.</param>
    /// <param name="displayName">Optional display text for authoring surfaces.</param>
    /// <param name="description">Optional human-readable node description.</param>
    /// <param name="disabled">Whether this node is declared disabled.</param>
    /// <param name="parameters">The extensible node-specific parameter object.</param>
    /// <param name="policy">Optional future execution policy declarations.</param>
    public WorkflowNode(
        string id,
        string type,
        int typeVersion,
        string? displayName = null,
        string? description = null,
        bool disabled = false,
        JsonObject? parameters = null,
        WorkflowExecutionPolicy? policy = null)
    {
        Id = id;
        Type = type;
        TypeVersion = typeVersion;
        DisplayName = displayName;
        Description = description;
        Disabled = disabled;
        _parameters = parameters is null ? [] : (JsonObject)parameters.DeepClone();
        Policy = policy;
    }

    /// <summary>
    /// Gets the node identifier within the workflow document.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the namespace-style node type identifier.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the declared node type version.
    /// </summary>
    public int TypeVersion { get; }

    /// <summary>
    /// Gets optional display text for authoring surfaces.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Gets an optional human-readable node description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets a value indicating whether this node is declared disabled.
    /// </summary>
    public bool Disabled { get; }

    /// <summary>
    /// Gets a defensive copy of the node-specific parameter object.
    /// </summary>
    public JsonObject Parameters => (JsonObject)_parameters.DeepClone();

    /// <summary>
    /// Gets optional future execution policy declarations.
    /// </summary>
    public WorkflowExecutionPolicy? Policy { get; }
}
