using SkeletonKey.Execution;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources.Tests;

/// <summary>Covers host-owned resource reuse, health, recycle, and final disposal.</summary>
public sealed class HostResourceRegistryTests
{
    /// <summary>Verifies a healthy resource is reused and disposed exactly once with its host registry.</summary>
    [Fact]
    public async Task ReusesHealthyResourceAcrossRequests()
    {
        FakeProvider provider = new();
        WorkflowRuntimeHostResourceRegistry registry = new();
        WorkflowRuntimeResourceRequest firstRequest = Request("execution-a");
        WorkflowRuntimeResourceRequest secondRequest = Request("execution-b");

        IWorkflowRuntimeResourceInstance first = await registry.GetOrCreateAsync(firstRequest, provider);
        IWorkflowRuntimeResourceInstance second = await registry.GetOrCreateAsync(secondRequest, provider);

        Assert.Same(first, second);
        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(0, provider.Created[0].DisposeCount);

        await registry.DisposeAsync();

        Assert.Equal(1, provider.Created[0].DisposeCount);
    }

    /// <summary>Verifies a disconnected resource is disposed and recreated before reuse.</summary>
    [Fact]
    public async Task RecyclesDisconnectedResource()
    {
        FakeProvider provider = new();
        await using WorkflowRuntimeHostResourceRegistry registry = new();
        HealthResource first = Assert.IsType<HealthResource>(await registry.GetOrCreateAsync(Request("execution-a"), provider));
        first.Health = WorkflowRuntimeResourceHealthState.Disconnected;

        HealthResource second = Assert.IsType<HealthResource>(await registry.GetOrCreateAsync(Request("execution-b"), provider));

        Assert.NotSame(first, second);
        Assert.Equal(2, provider.CreateCount);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, second.DisposeCount);
    }

    /// <summary>Verifies an explicit recycle removes the current resource and the next request creates a new one.</summary>
    [Fact]
    public async Task ExplicitRecycleCreatesReplacement()
    {
        FakeProvider provider = new();
        await using WorkflowRuntimeHostResourceRegistry registry = new();
        HealthResource first = Assert.IsType<HealthResource>(await registry.GetOrCreateAsync(Request("execution-a"), provider));

        bool recycled = await registry.RecycleAsync("workflow", "state");
        HealthResource second = Assert.IsType<HealthResource>(await registry.GetOrCreateAsync(Request("execution-b"), provider));

        Assert.True(recycled);
        Assert.NotSame(first, second);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(2, provider.CreateCount);
    }

    private static WorkflowRuntimeResourceRequest Request(string executionId)
    {
        WorkflowResourceDefinition definition = new(
            "demo.host",
            WorkflowResourceLifetime.Host,
            WorkflowResourceAccessMode.Exclusive);
        return new WorkflowRuntimeResourceRequest(executionId, "invocation:" + executionId, "workflow", "state", definition);
    }

    private sealed class FakeProvider : IWorkflowRuntimeResourceProvider
    {
        public string Kind => "demo.host";

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public int CreateCount { get; private set; }

        public List<HealthResource> Created { get; } = [];

        public ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(
            WorkflowRuntimeResourceRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCount++;
            HealthResource resource = new(request.ResourceName, request.Definition.Access, "instance-" + CreateCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Created.Add(resource);
            return ValueTask.FromResult<IWorkflowRuntimeResourceInstance>(resource);
        }
    }

    private sealed class HealthResource(
        string resourceName,
        WorkflowResourceAccessMode access,
        string instanceId) : IWorkflowRuntimeResourceInstance, IWorkflowRuntimeResourceHealthParticipant
    {
        public WorkflowRuntimeResourceHealthState Health { get; set; } = WorkflowRuntimeResourceHealthState.Healthy;

        public int DisposeCount { get; private set; }

        public string ResourceName { get; } = resourceName;

        public string Kind => "demo.host";

        public string InstanceId { get; } = instanceId;

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public WorkflowResourceAccessMode Access { get; } = access;

        public INodeResourceHandle CreateHandle()
        {
            return new Handle(ResourceName, InstanceId);
        }

        public ValueTask<WorkflowRuntimeResourceHealthState> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Health);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Handle(string resourceName, string instanceId) : INodeResourceHandle
    {
        public string ResourceName { get; } = resourceName;

        public string Kind => "demo.host";

        public string InstanceId { get; } = instanceId;

        public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(Array.Empty<string>());

        public bool TryGetAdapter<TAdapter>(out TAdapter? adapter)
            where TAdapter : class
        {
            adapter = null;
            return false;
        }

        public TAdapter GetRequiredAdapter<TAdapter>()
            where TAdapter : class
        {
            throw new InvalidOperationException("No adapter is exposed by this test resource.");
        }
    }
}
