using System.Collections.ObjectModel;
using SkeletonKey.Execution;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Exposes planned node resource slots backed by runtime-created resource instances.
/// </summary>
public sealed class RuntimeNodeResourceAccessor : INodeResourceAccessor
{
    private readonly IReadOnlyDictionary<string, NodeResourceBinding> _bindingsBySlot;
    private readonly IReadOnlyDictionary<string, ResourceSlotState> _resourcesBySlot;

    /// <summary>
    /// Initializes a runtime node resource accessor.
    /// </summary>
    public RuntimeNodeResourceAccessor(
        IReadOnlyList<NodeResourceBinding>? bindings = null,
        IReadOnlyDictionary<string, IWorkflowRuntimeResourceInstance>? resourcesByName = null)
    {
        _bindingsBySlot = (bindings ?? Array.AsReadOnly(Array.Empty<NodeResourceBinding>()))
            .ToDictionary(static binding => binding.SlotName, StringComparer.Ordinal);
        Dictionary<string, ResourceSlotState> resourcesBySlot = new(StringComparer.Ordinal);
        foreach (NodeResourceBinding binding in _bindingsBySlot.Values)
        {
            if (resourcesByName is not null && resourcesByName.TryGetValue(binding.WorkflowResourceName, out IWorkflowRuntimeResourceInstance? resource))
            {
                resourcesBySlot[binding.SlotName] = new ResourceSlotState(resource);
            }
        }

        _resourcesBySlot = resourcesBySlot;
    }

    /// <inheritdoc />
    public IReadOnlyList<NodeResourceBinding> Bindings => new ReadOnlyCollection<NodeResourceBinding>([.. _bindingsBySlot.Values.OrderBy(static binding => binding.SlotName, StringComparer.Ordinal)]);

    /// <inheritdoc />
    public bool TryGetBinding(string slotName, out NodeResourceBinding? binding)
    {
        return _bindingsBySlot.TryGetValue(slotName, out binding);
    }

    /// <inheritdoc />
    public async ValueTask<INodeResourceLease> AcquireAsync(string slotName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_bindingsBySlot.TryGetValue(slotName, out NodeResourceBinding? binding))
        {
            throw new KeyNotFoundException("The requested node resource slot is not bound.");
        }

        if (!_resourcesBySlot.TryGetValue(slotName, out ResourceSlotState? state))
        {
            if (!binding.Required)
            {
                throw new InvalidOperationException("The optional node resource slot is not available.");
            }

            throw new InvalidOperationException("The required node resource slot is not available.");
        }

        await state.AcquireAsync(binding.Access, cancellationToken).ConfigureAwait(false);
        return new RuntimeResourceLease(state.Resource.CreateHandle(), binding.Access, () => state.Release(binding.Access));
    }

    private sealed class ResourceSlotState(IWorkflowRuntimeResourceInstance resource)
    {
        private readonly SemaphoreSlim _exclusive = new(1, 1);
        private readonly SemaphoreSlim _sharedGate = new(1, 1);
        private int _sharedCount;

        public IWorkflowRuntimeResourceInstance Resource { get; } = resource;

        public async ValueTask AcquireAsync(WorkflowResourceAccessMode access, CancellationToken cancellationToken)
        {
            if (access == WorkflowResourceAccessMode.Exclusive)
            {
                await _exclusive.WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await _sharedGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_sharedCount == 0)
                {
                    await _exclusive.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                _sharedCount++;
            }
            finally
            {
                _sharedGate.Release();
            }
        }

        public void Release(WorkflowResourceAccessMode access)
        {
            if (access == WorkflowResourceAccessMode.Exclusive)
            {
                _exclusive.Release();
                return;
            }

            _sharedGate.Wait();
            try
            {
                _sharedCount--;
                if (_sharedCount == 0)
                {
                    _exclusive.Release();
                }
            }
            finally
            {
                _sharedGate.Release();
            }
        }
    }
}
