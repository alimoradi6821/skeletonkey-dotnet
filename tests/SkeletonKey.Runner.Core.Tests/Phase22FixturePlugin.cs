using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Runtime.Plugins;
using SkeletonKey.Runtime.Resources;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Provides a bounded plugin fixture used by Phase 22 verification.</summary>
public sealed class Phase22FixturePlugin : ISkeletonKeyPlugin
{
    /// <inheritdoc />
    public string Id => "phase22.fixture";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyList<WorkflowNodeDefinition> NodeDefinitions { get; } =
    [
        new WorkflowNodeDefinition(
            "phase22.fixture.complete",
            1,
            displayName: "Phase 22 Complete",
            inputs: new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
            {
                ["main"] = new("main", WorkflowPortDirection.Input),
            },
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Terminal, terminal: true)),
    ];

    /// <inheritdoc />
    public IReadOnlyList<INodeHandler> NodeHandlers { get; } = [new Phase22CompleteHandler()];

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowRuntimeResourceProvider> ResourceProviders { get; } = [new Phase22ResourceProvider()];

    private sealed class Phase22CompleteHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("phase22.fixture.complete", 1);

        public ValueTask<NodeHandlerResult> ExecuteAsync(
            NodeExecutionRequest request,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NodeHandlerResult.Success());
        }
    }

    private sealed class Phase22ResourceProvider : IWorkflowRuntimeResourceProvider
    {
        public string Kind => "phase22.fixture.resource";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(["phase22.fixture.resource.read"]);

        public ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(
            WorkflowRuntimeResourceRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IWorkflowRuntimeResourceInstance instance = new WorkflowRuntimeResourceInstance(
                request.ResourceName,
                request.Definition.Kind,
                "phase22-fixture",
                request.Definition.Access,
                Capabilities);
            return ValueTask.FromResult(instance);
        }
    }
}

/// <summary>Provides an identity mismatch fixture for loader rejection tests.</summary>
public sealed class Phase22MismatchedPlugin : ISkeletonKeyPlugin
{
    /// <inheritdoc />
    public string Id => "phase22.wrong";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyList<WorkflowNodeDefinition> NodeDefinitions { get; } = Array.AsReadOnly(Array.Empty<WorkflowNodeDefinition>());

    /// <inheritdoc />
    public IReadOnlyList<INodeHandler> NodeHandlers { get; } = Array.AsReadOnly(Array.Empty<INodeHandler>());

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowRuntimeResourceProvider> ResourceProviders { get; } = Array.AsReadOnly(Array.Empty<IWorkflowRuntimeResourceProvider>());
}
