using SkeletonKey.Execution;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Represents a runtime-owned lease over a workflow resource instance.
/// </summary>
public sealed class RuntimeResourceLease : INodeResourceLease
{
    private readonly Action _release;
    private bool _disposed;

    /// <summary>
    /// Initializes a runtime resource lease.
    /// </summary>
    public RuntimeResourceLease(INodeResourceHandle resource, WorkflowResourceAccessMode access, Action release)
    {
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        Access = access;
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    /// <inheritdoc />
    public INodeResourceHandle Resource { get; }

    /// <inheritdoc />
    public WorkflowResourceAccessMode Access { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _release();
        }

        return ValueTask.CompletedTask;
    }
}
