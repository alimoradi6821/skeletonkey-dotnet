using System.Collections.ObjectModel;
using SkeletonKey.Catalog;
using SkeletonKey.Handlers;
using SkeletonKey.Runtime.Resources;

namespace SkeletonKey.Runner.Core.Plugins;

/// <summary>Describes one validated local plugin package without exposing its executable instance.</summary>
public sealed record SkeletonKeyPluginDescriptor(
    string Id,
    string Version,
    string AssemblyFileName,
    string EntryType,
    int NodeDefinitionCount,
    int NodeHandlerCount,
    int ResourceProviderCount);

/// <summary>Contains immutable contributions loaded from explicit local plugin directories.</summary>
public sealed class SkeletonKeyPluginLoadResult
{
    private static readonly SkeletonKeyPluginLoadResult _empty = new([], [], [], []);

    /// <summary>Initializes a validated plugin load result.</summary>
    public SkeletonKeyPluginLoadResult(
        IReadOnlyList<SkeletonKeyPluginDescriptor> plugins,
        IReadOnlyList<WorkflowNodeDefinition> nodeDefinitions,
        IReadOnlyList<INodeHandler> nodeHandlers,
        IReadOnlyList<IWorkflowRuntimeResourceProvider> resourceProviders)
    {
        Plugins = new ReadOnlyCollection<SkeletonKeyPluginDescriptor>([.. plugins]);
        NodeDefinitions = new ReadOnlyCollection<WorkflowNodeDefinition>([.. nodeDefinitions]);
        NodeHandlers = new ReadOnlyCollection<INodeHandler>([.. nodeHandlers]);
        ResourceProviders = new ReadOnlyCollection<IWorkflowRuntimeResourceProvider>([.. resourceProviders]);
    }

    /// <summary>Gets an empty plugin load result.</summary>
    public static SkeletonKeyPluginLoadResult Empty => _empty;

    /// <summary>Gets validated plugin descriptors in deterministic manifest order.</summary>
    public IReadOnlyList<SkeletonKeyPluginDescriptor> Plugins { get; }

    /// <summary>Gets all contributed node definitions.</summary>
    public IReadOnlyList<WorkflowNodeDefinition> NodeDefinitions { get; }

    /// <summary>Gets all contributed node handlers.</summary>
    public IReadOnlyList<INodeHandler> NodeHandlers { get; }

    /// <summary>Gets all contributed resource providers.</summary>
    public IReadOnlyList<IWorkflowRuntimeResourceProvider> ResourceProviders { get; }
}
