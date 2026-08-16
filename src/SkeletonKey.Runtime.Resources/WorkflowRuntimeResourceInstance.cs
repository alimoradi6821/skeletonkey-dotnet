using System.Collections.ObjectModel;
using SkeletonKey.Execution;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Provides a simple immutable runtime resource instance wrapper for provider-owned adapters.
/// </summary>
public sealed class WorkflowRuntimeResourceInstance : IWorkflowRuntimeResourceInstance
{
    private readonly object? _adapter;
    private readonly IReadOnlyList<string> _capabilities;
    private readonly Func<ValueTask>? _disposeAsync;

    /// <summary>
    /// Initializes a runtime resource instance.
    /// </summary>
    public WorkflowRuntimeResourceInstance(
        string resourceName,
        string kind,
        string instanceId,
        WorkflowResourceAccessMode access,
        IReadOnlyList<string>? capabilities = null,
        object? adapter = null,
        Func<ValueTask>? disposeAsync = null)
    {
        ResourceName = resourceName;
        Kind = kind;
        InstanceId = instanceId;
        Access = access;
        _capabilities = capabilities is null ? Array.AsReadOnly(Array.Empty<string>()) : new ReadOnlyCollection<string>([.. capabilities]);
        _adapter = adapter;
        _disposeAsync = disposeAsync;
    }

    /// <inheritdoc />
    public string ResourceName { get; }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public string InstanceId { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Capabilities => new ReadOnlyCollection<string>([.. _capabilities]);

    /// <inheritdoc />
    public WorkflowResourceAccessMode Access { get; }

    /// <inheritdoc />
    public INodeResourceHandle CreateHandle()
    {
        return new RuntimeNodeResourceHandle(ResourceName, Kind, InstanceId, Capabilities, _adapter);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _disposeAsync is null ? ValueTask.CompletedTask : _disposeAsync();
    }

    private sealed class RuntimeNodeResourceHandle(
        string resourceName,
        string kind,
        string instanceId,
        IReadOnlyList<string> capabilities,
        object? adapter) : INodeResourceHandle
    {
        public string ResourceName { get; } = resourceName;

        public string Kind { get; } = kind;

        public string InstanceId { get; } = instanceId;

        public IReadOnlyList<string> Capabilities { get; } = new ReadOnlyCollection<string>([.. capabilities]);

        public bool TryGetAdapter<TAdapter>(out TAdapter? typedAdapter)
            where TAdapter : class
        {
            typedAdapter = adapter as TAdapter;
            return typedAdapter is not null;
        }

        public TAdapter GetRequiredAdapter<TAdapter>()
            where TAdapter : class
        {
            return TryGetAdapter(out TAdapter? typedAdapter) && typedAdapter is not null
                ? typedAdapter
                : throw new InvalidOperationException("The requested resource adapter is not available.");
        }
    }
}
