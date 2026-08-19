using SkeletonKey.Runtime.Resources;

namespace SkeletonKey.Runtime;

/// <summary>Describes one resource that was live at a durable checkpoint boundary.</summary>
public sealed class WorkflowCheckpointResource
{
    /// <summary>Initializes an immutable resource checkpoint entry.</summary>
    public WorkflowCheckpointResource(
        string resourceName,
        string kind,
        bool isResumable,
        WorkflowRuntimeResourceCheckpointState? state = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (isResumable != (state is not null))
        {
            throw new ArgumentException("A resumable resource checkpoint must contain exactly one state payload.", nameof(state));
        }

        ResourceName = resourceName;
        Kind = kind;
        IsResumable = isResumable;
        State = state;
    }

    /// <summary>Gets the workflow resource declaration name.</summary>
    public string ResourceName { get; }

    /// <summary>Gets the provider-neutral resource kind.</summary>
    public string Kind { get; }

    /// <summary>Gets whether the resource can be reconstructed safely.</summary>
    public bool IsResumable { get; }

    /// <summary>Gets the provider-owned reconstruction state.</summary>
    public WorkflowRuntimeResourceCheckpointState? State { get; }
}
