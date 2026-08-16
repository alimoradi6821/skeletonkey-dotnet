using System.Collections.ObjectModel;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Execution;

/// <summary>
/// Describes one planned binding between a node resource slot and a workflow resource declaration.
/// </summary>
/// <remarks>
/// Bindings are produced by future planning or runtime work. They are immutable and do not resolve, create, lock, or acquire resources.
/// </remarks>
public sealed class NodeResourceBinding
{
    private readonly IReadOnlyList<string> _capabilities;

    /// <summary>
    /// Initializes a new node resource binding.
    /// </summary>
    /// <param name="slotName">The node resource slot name declared by the node definition.</param>
    /// <param name="workflowResourceName">The workflow resource declaration name bound to the slot.</param>
    /// <param name="kind">The host-neutral workflow resource kind.</param>
    /// <param name="access">The declared access mode for the resource lease.</param>
    /// <param name="capabilities">The ordered capabilities expected from the bound resource.</param>
    /// <param name="required">Whether the resource binding is required for normal node execution.</param>
    public NodeResourceBinding(
        string slotName,
        string workflowResourceName,
        string kind,
        WorkflowResourceAccessMode access,
        IReadOnlyList<string>? capabilities = null,
        bool required = true)
    {
        SlotName = slotName;
        WorkflowResourceName = workflowResourceName;
        Kind = kind;
        Access = access;
        _capabilities = capabilities is null ? Array.AsReadOnly(Array.Empty<string>()) : new ReadOnlyCollection<string>([.. capabilities]);
        Required = required;
    }

    /// <summary>
    /// Gets the node resource slot name declared by the node definition.
    /// </summary>
    public string SlotName { get; }

    /// <summary>
    /// Gets the workflow resource declaration name bound to the slot.
    /// </summary>
    public string WorkflowResourceName { get; }

    /// <summary>
    /// Gets the host-neutral workflow resource kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the declared access mode for the resource lease.
    /// </summary>
    public WorkflowResourceAccessMode Access { get; }

    /// <summary>
    /// Gets a defensive copy of ordered capabilities expected from the bound resource.
    /// </summary>
    public IReadOnlyList<string> Capabilities => new ReadOnlyCollection<string>([.. _capabilities]);

    /// <summary>
    /// Gets a value indicating whether the resource binding is required for normal node execution.
    /// </summary>
    public bool Required { get; }
}
