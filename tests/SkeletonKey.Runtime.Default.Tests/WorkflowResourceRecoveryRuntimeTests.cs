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

/// <summary>Covers Phase 24 safe-boundary runtime resource reconstruction.</summary>
public sealed class WorkflowResourceRecoveryRuntimeTests
{
    /// <summary>Verifies a live resource is captured, reconstructed, and not recreated from empty state during resume.</summary>
    [Fact]
    public async Task ResumeReconstructsPreviouslyActivatedResource()
    {
        ResumableResourceProvider provider = new();
        ResourceHandler write = new("demo.resource.write", valueToWrite: 42);
        ResourceHandler read = new("demo.resource.read", expectedValue: 42);
        WorkflowDocument workflow = Workflow();
        FailingCheckpointStore firstStore = new(failFromSave: 6);

        WorkflowRuntimeResult interrupted = await Runtime(provider, write, read).ExecuteAsync(Request(workflow, firstStore));

        Assert.Equal(WorkflowExecutionStatus.Failed, interrupted.Result.Status);
        WorkflowExecutionCheckpoint safe = Assert.IsType<WorkflowExecutionCheckpoint>(firstStore.Current);
        WorkflowCheckpointResource savedResource = Assert.Single(safe.Resources);
        Assert.True(savedResource.IsResumable);
        Assert.Equal(42, savedResource.State!.Payload["value"]!.GetValue<int>());
        Assert.Equal(1, write.ExecutionCount);
        Assert.Equal(0, read.ExecutionCount);

        FailingCheckpointStore resumedStore = new(safe);
        WorkflowRuntimeResult resumed = await Runtime(provider, write, read).ExecuteAsync(Request(workflow, resumedStore, safe));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, resumed.Result.Status);
        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(1, provider.RestoreCount);
        Assert.Equal(1, write.ExecutionCount);
        Assert.Equal(1, read.ExecutionCount);
        Assert.True(resumedStore.Current!.IsTerminal);
    }

    /// <summary>Verifies an activated resource without checkpoint participation remains explicitly non-resumable.</summary>
    [Fact]
    public async Task ResumeRejectsNonResumableActivatedResource()
    {
        NonResumableResourceProvider provider = new();
        ResourceHandler write = new("demo.resource.write", valueToWrite: 7);
        ResourceHandler read = new("demo.resource.read", expectedValue: 7);
        WorkflowDocument workflow = Workflow();
        FailingCheckpointStore firstStore = new(failFromSave: 6);
        await Runtime(provider, write, read).ExecuteAsync(Request(workflow, firstStore));
        WorkflowExecutionCheckpoint safe = Assert.IsType<WorkflowExecutionCheckpoint>(firstStore.Current);

        WorkflowRuntimeResult resumed = await Runtime(provider, write, read).ExecuteAsync(Request(workflow, new FailingCheckpointStore(safe), safe));

        Assert.Equal(WorkflowExecutionStatus.Failed, resumed.Result.Status);
        Assert.Equal(WorkflowCheckpointErrorCodes.ResourceResumeNotSupported, resumed.Result.Error!.Code);
        Assert.False(Assert.Single(safe.Resources).IsResumable);
    }

    private static WorkflowExecutionRequest Request(WorkflowDocument workflow, IWorkflowCheckpointStore store, WorkflowExecutionCheckpoint? checkpoint = null)
    {
        return new WorkflowExecutionRequest(workflow, "resource-recovery", "resource-plan", checkpointStore: store, resumeCheckpoint: checkpoint);
    }

    private static DefaultWorkflowRuntime Runtime(IWorkflowRuntimeResourceProvider provider, params ResourceHandler[] handlers)
    {
        WorkflowNodeDefinition[] definitions = handlers.Select(handler => new WorkflowNodeDefinition(
            handler.Definition.Type,
            handler.Definition.Version,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Ports(WorkflowPortDirection.Output, "next"),
            resources: new Dictionary<string, WorkflowNodeResourceRequirement>(StringComparer.Ordinal)
            {
                ["state"] = new("state", "demo.resumable"),
            })).ToArray();
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. definitions]);
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver([.. BuiltInRuntimeHandlers.Create(), .. handlers]),
            new NodeParameterMaterializer(),
            resourceProviders: [provider]);
    }

    private static WorkflowDocument Workflow()
    {
        JsonObject binding = new() { ["$resource"] = new JsonObject { ["name"] = "state" } };
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            new("write", "demo.resource.write", 1, parameters: new JsonObject { ["state"] = binding.DeepClone() }),
            new("read", "demo.resource.read", 1, parameters: new JsonObject { ["state"] = binding.DeepClone() }),
            new("done", "core.return", 1, parameters: new JsonObject
            {
                ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" },
            }),
        ];
        WorkflowConnection[] connections =
        [
            Connect("start", "main", "write", "main"),
            Connect("write", "next", "read", "main"),
            Connect("read", "next", "done", "main"),
        ];
        return new WorkflowDocument(
            id: "resource-recovery-workflow",
            name: "Resource Recovery Workflow",
            nodes: nodes,
            connections: connections,
            resources: new Dictionary<string, WorkflowResourceDefinition>(StringComparer.Ordinal)
            {
                ["state"] = new("demo.resumable"),
            });
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(WorkflowPortDirection direction, string name)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal) { [name] = new(name, direction) };
    }

    private sealed class ResourceHandler : INodeHandler
    {
        private readonly int? _valueToWrite;
        private readonly int? _expectedValue;

        public ResourceHandler(string type, int? valueToWrite = null, int? expectedValue = null)
        {
            Definition = new WorkflowNodeDefinitionKey(type, 1);
            _valueToWrite = valueToWrite;
            _expectedValue = expectedValue;
        }

        public WorkflowNodeDefinitionKey Definition { get; }

        public int ExecutionCount { get; private set; }

        public async ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            await using INodeResourceLease lease = await context.Resources.AcquireAsync("state", cancellationToken);
            MutableState state = lease.Resource.GetRequiredAdapter<MutableState>();
            ExecutionCount++;
            if (_valueToWrite is int value)
            {
                state.Value = value;
            }

            if (_expectedValue is int expected)
            {
                Assert.Equal(expected, state.Value);
            }

            return NodeHandlerResult.Success(new NodeHandlerOutputs(["next"]));
        }
    }

    private sealed class ResumableResourceProvider : IWorkflowRuntimeResourceRecoveryProvider
    {
        public string Kind => "demo.resumable";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public int CreateCount { get; private set; }

        public int RestoreCount { get; private set; }

        public ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(WorkflowRuntimeResourceRequest request, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return ValueTask.FromResult<IWorkflowRuntimeResourceInstance>(new ResumableResource(request.ResourceName, new MutableState()));
        }

        public ValueTask<IWorkflowRuntimeResourceInstance> RestoreAsync(WorkflowRuntimeResourceRequest request, WorkflowRuntimeResourceCheckpointState state, CancellationToken cancellationToken = default)
        {
            RestoreCount++;
            return ValueTask.FromResult<IWorkflowRuntimeResourceInstance>(new ResumableResource(
                request.ResourceName,
                new MutableState { Value = state.Payload["value"]!.GetValue<int>() }));
        }
    }

    private sealed class NonResumableResourceProvider : IWorkflowRuntimeResourceProvider
    {
        public string Kind => "demo.resumable";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(WorkflowRuntimeResourceRequest request, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IWorkflowRuntimeResourceInstance>(new NonResumableResource(request.ResourceName, new MutableState()));
        }
    }

    private sealed class ResumableResource(string resourceName, MutableState state) : Resource(resourceName, state), IWorkflowRuntimeResourceCheckpointParticipant
    {
        public ValueTask<WorkflowRuntimeResourceCheckpointState?> CaptureCheckpointStateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkflowRuntimeResourceCheckpointState checkpoint = new("0.1", new JsonObject { ["value"] = State.Value });
            return ValueTask.FromResult<WorkflowRuntimeResourceCheckpointState?>(checkpoint);
        }
    }

    private sealed class NonResumableResource(string resourceName, MutableState state) : Resource(resourceName, state);

    private abstract class Resource : IWorkflowRuntimeResourceInstance
    {
        protected Resource(string resourceName, MutableState state)
        {
            ResourceName = resourceName;
            State = state;
        }

        protected MutableState State { get; }

        public string ResourceName { get; }

        public string Kind => "demo.resumable";

        public string InstanceId => "demo:resumable";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public WorkflowResourceAccessMode Access => WorkflowResourceAccessMode.Exclusive;

        public INodeResourceHandle CreateHandle()
        {
            return new Handle(ResourceName, State);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Handle(string resourceName, MutableState state) : INodeResourceHandle
    {
        public string ResourceName { get; } = resourceName;

        public string Kind => "demo.resumable";

        public string InstanceId => "demo:resumable";

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
        public int Value { get; set; }
    }

    private sealed class FailingCheckpointStore : IWorkflowCheckpointStore
    {
        private readonly int? _failFromSave;
        private int _saves;

        public FailingCheckpointStore(int? failFromSave = null)
        {
            _failFromSave = failFromSave;
        }

        public FailingCheckpointStore(WorkflowExecutionCheckpoint initial)
        {
            Current = initial;
        }

        public WorkflowExecutionCheckpoint? Current { get; private set; }

        public ValueTask<WorkflowExecutionCheckpoint?> LoadAsync(string executionId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask SaveAsync(WorkflowExecutionCheckpoint checkpoint, long expectedRevision, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _saves++;
            if (_failFromSave is not null && _saves >= _failFromSave.Value)
            {
                throw new WorkflowCheckpointStoreException(WorkflowCheckpointErrorCodes.StoreFailure, "Simulated process stop.");
            }

            Assert.Equal(Current?.Revision ?? 0, expectedRevision);
            Current = checkpoint;
            return ValueTask.CompletedTask;
        }
    }
}
