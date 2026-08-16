using SkeletonKey.Execution;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Represents one runtime-owned workflow resource instance.
/// </summary>
public interface IWorkflowRuntimeResourceInstance : IAsyncDisposable
{
    /// <summary>Gets the workflow resource declaration name.</summary>
    public string ResourceName { get; }

    /// <summary>Gets the provider-neutral resource kind.</summary>
    public string Kind { get; }

    /// <summary>Gets the provider-supplied resource instance identifier.</summary>
    public string InstanceId { get; }

    /// <summary>Gets ordered capabilities exposed by this instance.</summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>Gets the declared access mode for this instance.</summary>
    public WorkflowResourceAccessMode Access { get; }

    /// <summary>Creates the node-visible resource handle for one scoped lease.</summary>
    public INodeResourceHandle CreateHandle();
}
