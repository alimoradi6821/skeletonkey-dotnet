using SkeletonKey.Catalog;
using SkeletonKey.Handlers;
using SkeletonKey.Runtime.Resources;

namespace SkeletonKey.Runtime.Plugins;

/// <summary>
/// Exposes one explicitly loaded set of host-neutral runtime contributions.
/// </summary>
/// <remarks>
/// This contract does not discover assemblies, provide dependency injection, download packages, or create a security boundary.
/// A host decides how an implementation is located, verified, instantiated, and composed.
/// </remarks>
public interface ISkeletonKeyPlugin
{
    /// <summary>Gets the stable plugin identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the plugin version reported by the implementation.</summary>
    public string Version { get; }

    /// <summary>Gets the node definitions contributed by the plugin.</summary>
    public IReadOnlyList<WorkflowNodeDefinition> NodeDefinitions { get; }

    /// <summary>Gets the exact node handlers contributed by the plugin.</summary>
    public IReadOnlyList<INodeHandler> NodeHandlers { get; }

    /// <summary>Gets the runtime resource providers contributed by the plugin.</summary>
    public IReadOnlyList<IWorkflowRuntimeResourceProvider> ResourceProviders { get; }
}
