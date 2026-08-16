using System.Reflection;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Execution.Tests;

/// <summary>
/// Verifies scoped node resource access contracts.
/// </summary>
public sealed class ResourceContractTests
{
    /// <summary>
    /// Verifies resource bindings preserve slot identity and capability order.
    /// </summary>
    [Fact]
    public void ResourceBindingPreservesSlotIdentityAndCapabilities()
    {
        List<string> capabilities = ["read", "write"];
        NodeResourceBinding binding = new("browser", "primary-browser", "browser", WorkflowResourceAccessMode.Exclusive, capabilities, required: false);

        capabilities.Add("late");
        Assert.Equal("browser", binding.SlotName);
        Assert.Equal("primary-browser", binding.WorkflowResourceName);
        Assert.Equal("browser", binding.Kind);
        Assert.Equal(WorkflowResourceAccessMode.Exclusive, binding.Access);
        Assert.Equal(["read", "write"], binding.Capabilities);
        Assert.False(binding.Required);
    }

    /// <summary>
    /// Verifies accessor contracts expose slot-based access only.
    /// </summary>
    [Fact]
    public void AccessorInterfaceExposesSlotBasedAccessOnly()
    {
        MethodInfo[] methods = typeof(INodeResourceAccessor).GetMethods();

        Assert.Contains(methods, static method => method.Name == nameof(INodeResourceAccessor.TryGetBinding));
        Assert.Contains(methods, static method => method.Name == nameof(INodeResourceAccessor.AcquireAsync));
        Assert.DoesNotContain(methods, static method => method.Name.Contains("WorkflowResource", StringComparison.Ordinal) && method.Name.Contains("Get", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies leases are asynchronously disposable and handles expose explicit typed adapter access.
    /// </summary>
    [Fact]
    public void LeaseAndHandleExposeScopedAdapterContract()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(INodeResourceLease)));
        Assert.Contains(typeof(INodeResourceHandle).GetMethods(), static method => method.Name == nameof(INodeResourceHandle.TryGetAdapter) && method.IsGenericMethod);
        Assert.Contains(typeof(INodeResourceHandle).GetMethods(), static method => method.Name == nameof(INodeResourceHandle.GetRequiredAdapter) && method.IsGenericMethod);
        Assert.DoesNotContain(typeof(INodeResourceHandle).GetProperties(), static property => property.PropertyType == typeof(IServiceProvider));
    }
}
