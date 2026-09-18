namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Owns host-lifetime runtime resources across independent workflow executions.
/// </summary>
public interface IWorkflowRuntimeHostResourceRegistry : IAsyncDisposable
{
    /// <summary>
    /// Gets an existing reusable host resource or creates a replacement through the supplied provider.
    /// </summary>
    public ValueTask<IWorkflowRuntimeResourceInstance> GetOrCreateAsync(
        WorkflowRuntimeResourceRequest request,
        IWorkflowRuntimeResourceProvider provider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly removes and disposes one host resource when present.
    /// </summary>
    public ValueTask<bool> RecycleAsync(
        string workflowId,
        string resourceName,
        CancellationToken cancellationToken = default);
}
