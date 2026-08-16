using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SkeletonKey.Workflow.Resources;

/// <summary>
/// Describes a provider-neutral resource requirement declared by a workflow document.
/// </summary>
/// <remarks>
/// The definition is immutable, defensively copies capability lists, defensively clones JSON
/// constraints, and stores no live resource, host service, or execution object.
/// </remarks>
public sealed class WorkflowResourceDefinition
{
    private static readonly IReadOnlyList<string> _emptyCapabilities = Array.AsReadOnly(Array.Empty<string>());
    private readonly JsonObject? _constraints;

    /// <summary>
    /// Initializes a workflow resource requirement.
    /// </summary>
    /// <param name="kind">The dotted resource kind identifier.</param>
    /// <param name="lifetime">The requested resource lifetime.</param>
    /// <param name="access">The requested concurrent access contract.</param>
    /// <param name="required">Whether the resource is required for future execution.</param>
    /// <param name="capabilities">Ordered capability identifiers required from a provider.</param>
    /// <param name="constraints">Optional provider-neutral constraint JSON.</param>
    /// <param name="description">Optional human-readable description.</param>
    public WorkflowResourceDefinition(
        string kind,
        WorkflowResourceLifetime lifetime = WorkflowResourceLifetime.Invocation,
        WorkflowResourceAccessMode access = WorkflowResourceAccessMode.Exclusive,
        bool required = true,
        IReadOnlyList<string>? capabilities = null,
        JsonObject? constraints = null,
        string? description = null)
    {
        Kind = kind;
        Lifetime = lifetime;
        Access = access;
        Required = required;
        Capabilities = capabilities is null ? _emptyCapabilities : Array.AsReadOnly([.. capabilities]);
        _constraints = constraints is null ? null : (JsonObject)constraints.DeepClone();
        Description = description;
    }

    /// <summary>
    /// Gets the provider-neutral dotted resource kind identifier.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the declarative lifetime requirement for future host resource resolution.
    /// </summary>
    public WorkflowResourceLifetime Lifetime { get; }

    /// <summary>
    /// Gets the declarative concurrent access contract for future host scheduling.
    /// </summary>
    public WorkflowResourceAccessMode Access { get; }

    /// <summary>
    /// Gets a value indicating whether future hosts must resolve this resource before execution.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets ordered provider capability identifiers without assigning runtime meaning to plugin-defined values.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Gets a defensive clone of optional provider-neutral constraint JSON.
    /// </summary>
    public JsonObject? Constraints => _constraints is null ? null : (JsonObject)_constraints.DeepClone();

    /// <summary>
    /// Gets an optional human-readable resource description.
    /// </summary>
    public string? Description { get; }
}
