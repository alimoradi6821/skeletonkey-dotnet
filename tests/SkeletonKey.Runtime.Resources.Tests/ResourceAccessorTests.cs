using SkeletonKey.Execution;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources.Tests;

/// <summary>
/// Covers runtime resource leases and node accessors.
/// </summary>
public sealed class ResourceAccessorTests
{
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
