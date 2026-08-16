using SkeletonKey.Locators;

namespace SkeletonKey.Execution;

/// <summary>
/// Defines the host-neutral execution context passed to one node handler invocation.
/// </summary>
/// <remarks>
/// The context exposes only exact identity, runtime-owned event writing, and scoped resource access.
/// Cancellation is supplied explicitly to asynchronous methods and is not stored as mutable context state.
/// </remarks>
public interface INodeExecutionContext
{
    /// <summary>
    /// Gets the exact node execution attempt identity.
    /// </summary>
    public NodeExecutionIdentity Identity { get; }

    /// <summary>
    /// Gets the runtime-owned event writer for log, progress, and streamed output observations.
    /// </summary>
    public INodeExecutionEventWriter Events { get; }

    /// <summary>
    /// Gets scoped access to resources declared by the node definition and bound by a future runtime.
    /// </summary>
    public INodeResourceAccessor Resources { get; }

    /// <summary>
    /// Gets scoped access to resolved locator plans declared by the node definition.
    /// </summary>
    public INodeLocatorAccessor Locators { get; }
}
