namespace SkeletonKey.Execution;

/// <summary>
/// Represents a scoped host-neutral handle for one acquired workflow resource.
/// </summary>
/// <remarks>
/// Adapter access is explicit and scoped to the declared resource slot. It is not a global service locator.
/// Provider-specific handler projects may request explicit adapter interfaces or provider types.
/// </remarks>
public interface INodeResourceHandle
{
    /// <summary>
    /// Gets the workflow resource declaration name.
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// Gets the host-neutral resource kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the provider-supplied resource instance identifier.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets ordered resource capabilities exposed by this handle.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Attempts to get an explicit typed adapter for this scoped resource.
    /// </summary>
    /// <typeparam name="TAdapter">The explicit adapter interface or provider type requested by the handler.</typeparam>
    /// <param name="adapter">When successful, receives the scoped adapter instance.</param>
    /// <returns><see langword="true" /> when the adapter is available; otherwise, <see langword="false" />.</returns>
    public bool TryGetAdapter<TAdapter>(
        out TAdapter? adapter)
        where TAdapter : class;

    /// <summary>
    /// Gets a required explicit typed adapter for this scoped resource.
    /// </summary>
    /// <typeparam name="TAdapter">The explicit adapter interface or provider type requested by the handler.</typeparam>
    /// <returns>The scoped adapter instance.</returns>
    public TAdapter GetRequiredAdapter<TAdapter>()
        where TAdapter : class;
}
