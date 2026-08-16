using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Planning;

/// <summary>
/// Describes a workflow resource declaration as seen by an execution plan.
/// </summary>
public sealed class WorkflowExecutionPlanResource
{
    private static readonly IReadOnlyList<string> _emptyCapabilities = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>
    /// Initializes a plan resource declaration.
    /// </summary>
    /// <param name="name">The workflow resource name.</param>
    /// <param name="kind">The workflow resource kind.</param>
    /// <param name="lifetime">The declared resource lifetime.</param>
    /// <param name="access">The declared resource access mode.</param>
    /// <param name="required">Whether the resource is required for execution.</param>
    /// <param name="capabilities">Ordered required resource capabilities.</param>
    public WorkflowExecutionPlanResource(
        string name,
        string kind,
        WorkflowResourceLifetime lifetime,
        WorkflowResourceAccessMode access,
        bool required,
        IReadOnlyList<string>? capabilities = null)
    {
        Name = name;
        Kind = kind;
        Lifetime = lifetime;
        Access = access;
        Required = required;
        Capabilities = capabilities is null ? _emptyCapabilities : Array.AsReadOnly([.. capabilities]);
    }

    /// <summary>
    /// Gets the workflow resource name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the workflow resource kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the declared resource lifetime.
    /// </summary>
    public WorkflowResourceLifetime Lifetime { get; }

    /// <summary>
    /// Gets the declared resource access mode.
    /// </summary>
    public WorkflowResourceAccessMode Access { get; }

    /// <summary>
    /// Gets a value indicating whether the resource is required for execution.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets ordered required resource capabilities.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; }
}
