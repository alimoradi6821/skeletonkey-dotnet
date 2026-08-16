using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Execution;

/// <summary>
/// Represents an asynchronously disposable lease over one scoped resource handle.
/// </summary>
/// <remarks>
/// Lease lifetime is owned by the handler call and future runtime. Handles and leases must not be serialized into workflow documents or events.
/// </remarks>
public interface INodeResourceLease : IAsyncDisposable
{
    /// <summary>
    /// Gets the resource handle scoped to the acquired slot.
    /// </summary>
    public INodeResourceHandle Resource { get; }

    /// <summary>
    /// Gets the access mode granted for this lease.
    /// </summary>
    public WorkflowResourceAccessMode Access { get; }
}
