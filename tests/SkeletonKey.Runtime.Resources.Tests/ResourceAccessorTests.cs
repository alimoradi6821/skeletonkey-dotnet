using System.Text.Json.Nodes;
using SkeletonKey.Execution;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources.Tests;

/// <summary>
/// Covers runtime resource leases and node accessors.
/// </summary>
public sealed class ResourceAccessorTests
{
    /// <summary>Verifies provider state owns its input and never exposes mutable internal JSON.</summary>
    [Fact]
    public void ResourceCheckpointStateDefensivelyClonesPayload()
    {
        JsonObject source = new() { ["value"] = 42 };
        WorkflowRuntimeResourceCheckpointState state = new("0.1", source);

        source["value"] = 7;
        JsonObject firstRead = state.Payload;
        firstRead["value"] = 9;

        Assert.Equal(42, state.Payload["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies node resource accessors expose bindings and typed adapters through leases.
    /// </summary>
    [Fact]
    public async Task AcquiresBoundResourceLease()
    {
        object adapter = new();
        WorkflowRuntimeResourceInstance resource = new("resource", "demo.resource", "instance", WorkflowResourceAccessMode.Exclusive, adapter: adapter);
        RuntimeNodeResourceAccessor accessor = new(
            [new NodeResourceBinding("slot", "resource", "demo.resource", WorkflowResourceAccessMode.Exclusive)],
            new Dictionary<string, IWorkflowRuntimeResourceInstance>(StringComparer.Ordinal)
            {
                ["resource"] = resource,
            });

        await using INodeResourceLease lease = await accessor.AcquireAsync("slot");

        Assert.True(accessor.TryGetBinding("slot", out NodeResourceBinding? binding));
        Assert.Equal("resource", binding!.WorkflowResourceName);
        Assert.Same(adapter, lease.Resource.GetRequiredAdapter<object>());
    }

    /// <summary>
    /// Verifies resource sets dispose owned instances.
    /// </summary>
    [Fact]
    public async Task ResourceSetDisposesInstances()
    {
        bool disposed = false;
        WorkflowRuntimeResourceSet resources = new(new Dictionary<string, IWorkflowRuntimeResourceInstance>(StringComparer.Ordinal)
        {
            ["resource"] = new WorkflowRuntimeResourceInstance("resource", "demo.resource", "instance", WorkflowResourceAccessMode.Shared, disposeAsync: () =>
            {
                disposed = true;
                return ValueTask.CompletedTask;
            }),
        });

        await resources.DisposeAsync();

        Assert.True(disposed);
    }
}
