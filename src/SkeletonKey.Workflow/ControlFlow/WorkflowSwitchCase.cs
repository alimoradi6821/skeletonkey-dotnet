using System.Text.Json.Nodes;

namespace SkeletonKey.Workflow.ControlFlow;

/// <summary>
/// Represents one immutable graph-native switch case declaration.
/// </summary>
public sealed class WorkflowSwitchCase
{
    private readonly JsonNode? _when;

    /// <summary>
    /// Initializes a new switch case contract.
    /// </summary>
    /// <param name="id">The case output port identifier.</param>
    /// <param name="when">The workflow value expected to resolve to a boolean in a future runtime.</param>
    /// <param name="description">Optional human-readable case description.</param>
    public WorkflowSwitchCase(string id, JsonNode? when, string? description = null)
    {
        Id = id;
        _when = when?.DeepClone();
        Description = description;
    }

    /// <summary>
    /// Gets the case output port identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets a defensive copy of the future boolean condition workflow value.
    /// </summary>
    public JsonNode? When => _when?.DeepClone();

    /// <summary>
    /// Gets optional human-readable case description.
    /// </summary>
    public string? Description { get; }
}
