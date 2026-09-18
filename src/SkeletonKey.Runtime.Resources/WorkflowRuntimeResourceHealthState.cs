namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Describes whether a host-owned runtime resource can continue to be reused.
/// </summary>
public enum WorkflowRuntimeResourceHealthState
{
    /// <summary>The resource is reusable without intervention.</summary>
    Healthy,

    /// <summary>The resource is usable but the provider has observed a non-fatal degradation.</summary>
    Degraded,

    /// <summary>The resource is no longer connected and should be recycled before reuse.</summary>
    Disconnected,

    /// <summary>The current resource instance is permanently unusable and should be replaced.</summary>
    Unrecoverable,
}

/// <summary>
/// Allows a runtime resource instance to expose provider-neutral host health.
/// </summary>
public interface IWorkflowRuntimeResourceHealthParticipant
{
    /// <summary>Gets the current reusable health state without creating a replacement resource.</summary>
    public ValueTask<WorkflowRuntimeResourceHealthState> GetHealthAsync(CancellationToken cancellationToken = default);
}
