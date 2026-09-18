using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Materialization;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Default.Tests;

/// <summary>Covers Phase 2 host-lifetime resource ownership through the real runtime boundary.</summary>
public sealed class HostLifetimeResourceRuntimeTests
{
    /// <summary>Verifies one host resource is reused across two independent runtime executions and disposed only at host shutdown.</summary>
    [Fact]
    public async Task ReusesResourceAcrossIndependentExecutions()
    {
        HostProvider provider = new();
        HostHandler handler = new();
        WorkflowDocument workflow = Workflow();
        WorkflowRuntimeHostResourceRegistry registry = new();

        WorkflowRuntimeResult first = await Runtime(provider, handler, registry).ExecuteAsync(Request(workflow, "execution-a"));
        WorkflowRuntimeResult second = await Runtime(provider, handler, registry).ExecuteAsync(Request(workflow, "execution-b"));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, first.Result.Status);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, second.Result.Status);
        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(2, handler.ExecutionCount);
        Assert.Equal(2, handler.SeenStates.Count);
        Assert.Same(handler.SeenStates[0], handler.SeenStates[1]);
        Assert.Equal(0, provider.Created[0].DisposeCount);

        await registry.DisposeAsync();

        Assert.Equal(1, provider.Created[0].DisposeCount);
    }

    /// <summary>Verifies host-owned resources are excluded from execution checkpoints.</summary>
    [Fact]
    public async Task HostResourceIsNotCheckpointOwned()
    {
        HostProvider provider = new();
        HostHandler handler = new();
        WorkflowDocument workflow = Workflow();
        RecordingCheckpointStore store = new();
        await using WorkflowRuntimeHostResourceRegistry registry = new();

        WorkflowRuntimeResult result = await Runtime(provider, handler, registry).ExecuteAsync(
            new WorkflowExecutionRequest(workflow, "checkpoint-execution", "host-resource-plan", checkpointStore: store));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.NotNull(store.Current);
        Assert.True(store.Current!.IsTerminal);
        Assert.Empty(store.Current.Resources);
    }

    /// <summary>Verifies a host-lifetime declaration fails deterministically when no host registry is configured.</summary>
    [Fact]
    public async Task HostResourceRequiresRegistry()
    {
        HostProvider provider = new();
        HostHandler handler = new();
        WorkflowDocument workflow = Workflow();

        WorkflowRuntimeResult result = await Runtime(provider, handler, registry: null).ExecuteAsync(Request(workflow, "missing-registry"));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.RuntimeHostResourceRegistryUnavailable, result.Result.Error!.Code);
        Assert.Equal(0, provider.CreateCount);
    }

    private static WorkflowExecutionRequest Request(WorkflowDocument workflow, string executionId)
    {
        return new WorkflowExecutionRequest(workflow, executionId, "host-resource-plan");
    }

    private static DefaultWorkflowRuntime Runtime(
        IWorkflowRuntimeResourceProvider provider,
        HostHandler handler,
        IWorkflowRuntimeHostResourceRegistry? registry)
    {
        WorkflowNodeDefinition definition = new(
            handler.Definition.Type,
            handler.Definition.Version,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Ports(WorkflowPortDirection.Output, "next"),
            resources: new Dictionary<string, WorkflowNodeResourceRequirement>(StringComparer.Ordinal)
            {
                ["state"] = new("state", "demo.host"),
            });
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, definition]);
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver([.. BuiltInRuntimeHandlers.Create(), handler]),
            new NodeParameterMaterializer(),
            resourceProviders: [provider],
            hostResourceRegistry: registry);
    }

    private static WorkflowDocument Workflow()
    {
        JsonObject binding = new() { ["$resource"] = new JsonObject { ["name"] = "state" } };
        return new WorkflowDocument(
            id: "host-resource-workflow",
            name: "Host Resource Workflow",
            resources: new Dictionary<string, WorkflowResourceDefinition>(StringComparer.Ordinal)
            {
                ["state"] = new("demo.host", WorkflowResourceLifetime.Host, WorkflowResourceAccessMode.Exclusive),
            },
            nodes:
            [
                new("start", "core.start", 1),
                new("use", "demo.host.use", 1, parameters: new JsonObject { ["state"] = binding }),
                new("done", "core.return", 1, parameters: new JsonObject
                {
                    ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" },
                }),
            ],
            connections:
            [
                Connect("start", "main", "use", "main"),
                Connect("use", "next", "done", "main"),
            ]);
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(WorkflowPortDirection direction, string name)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, direction),
        };
    }

    private sealed class HostHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.host.use", 1);

        public int ExecutionCount { get; private set; }

        public List<MutableState> SeenStates { get; } = [];

        public async ValueTask<NodeHandlerResult> ExecuteAsync(
            NodeExecutionRequest request,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            await using INodeResourceLease lease = await context.Resources.AcquireAsync("state", cancellationToken);
            MutableState state = lease.Resource.GetRequiredAdapter<MutableState>();
            state.Uses++;
            SeenStates.Add(state);
            ExecutionCount++;
            return NodeHandlerResult.Success(new NodeHandlerOutputs(["next"]));
        }
    }

    private sealed class HostProvider : IWorkflowRuntimeResourceProvider
    {
        public string Kind => "demo.host";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public int CreateCount { get; private set; }

        public List<HostResource> Created { get; } = [];

        public ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(
            WorkflowRuntimeResourceRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCount++;
            HostResource resource = new(request.ResourceName, request.Definition.Access, new MutableState());
            Created.Add(resource);
            return ValueTask.FromResult<IWorkflowRuntimeResourceInstance>(resource);
        }
    }

    private sealed class HostResource(
        string resourceName,
        WorkflowResourceAccessMode access,
        MutableState state) : IWorkflowRuntimeResourceInstance, IWorkflowRuntimeResourceHealthParticipant
    {
        public int DisposeCount { get; private set; }

        public string ResourceName { get; } = resourceName;

        public string Kind => "demo.host";

        public string InstanceId => "demo:host";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public WorkflowResourceAccessMode Access { get; } = access;

        public INodeResourceHandle CreateHandle()
        {
            return new Handle(ResourceName, state);
        }

        public ValueTask<WorkflowRuntimeResourceHealthState> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(WorkflowRuntimeResourceHealthState.Healthy);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Handle(string resourceName, MutableState state) : INodeResourceHandle
    {
        public string ResourceName { get; } = resourceName;

        public string Kind => "demo.host";

        public string InstanceId => "demo:host";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public bool TryGetAdapter<TAdapter>(out TAdapter? adapter)
            where TAdapter : class
        {
            adapter = state as TAdapter;
            return adapter is not null;
        }

        public TAdapter GetRequiredAdapter<TAdapter>()
            where TAdapter : class
        {
            return TryGetAdapter(out TAdapter? adapter) && adapter is not null
                ? adapter
                : throw new InvalidOperationException("Adapter is unavailable.");
        }
    }

    private sealed class MutableState
    {
        public int Uses { get; set; }
    }

    private sealed class RecordingCheckpointStore : IWorkflowCheckpointStore
    {
        public WorkflowExecutionCheckpoint? Current { get; private set; }

        public ValueTask<WorkflowExecutionCheckpoint?> LoadAsync(string executionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Current);
        }

        public ValueTask SaveAsync(
            WorkflowExecutionCheckpoint checkpoint,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(Current?.Revision ?? 0, expectedRevision);
            Current = checkpoint;
            return ValueTask.CompletedTask;
        }
    }
}
