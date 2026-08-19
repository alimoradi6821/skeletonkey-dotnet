namespace SkeletonKey.Runtime.Resources;

/// <summary>Reconstructs a runtime resource from provider-owned checkpoint state.</summary>
public interface IWorkflowRuntimeResourceRecoveryProvider : IWorkflowRuntimeResourceProvider
{
    /// <summary>Restores one runtime resource instance for the supplied declaration and state.</summary>
    public ValueTask<IWorkflowRuntimeResourceInstance> RestoreAsync(
        WorkflowRuntimeResourceRequest request,
        WorkflowRuntimeResourceCheckpointState state,
        CancellationToken cancellationToken = default);
}
