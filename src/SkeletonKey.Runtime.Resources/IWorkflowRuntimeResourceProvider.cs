namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Creates host-neutral runtime resource instances for one resource kind.
/// </summary>
/// <remarks>
/// Providers are passed explicitly to the runtime. The contract does not imply discovery, dependency injection, assembly scanning, or plugin loading.
/// </remarks>
public interface IWorkflowRuntimeResourceProvider
{
    /// <summary>Gets the provider-neutral resource kind this provider supports.</summary>
    public string Kind { get; }

    /// <summary>Gets ordered provider-neutral capabilities supplied by the provider.</summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>Creates one runtime resource instance for the supplied declaration.</summary>
    public ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(
        WorkflowRuntimeResourceRequest request,
        CancellationToken cancellationToken = default);
}
