using System.Collections.ObjectModel;

namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Owns runtime resource instances for one execution or invocation lifetime and disposes them deterministically.
/// </summary>
public sealed class WorkflowRuntimeResourceSet : IAsyncDisposable
{
    private readonly Dictionary<string, IWorkflowRuntimeResourceInstance> _resources;

    /// <summary>
    /// Initializes a runtime resource set.
    /// </summary>
    public WorkflowRuntimeResourceSet(IReadOnlyDictionary<string, IWorkflowRuntimeResourceInstance>? resources = null)
    {
        _resources = resources is null ? new Dictionary<string, IWorkflowRuntimeResourceInstance>(StringComparer.Ordinal) : new Dictionary<string, IWorkflowRuntimeResourceInstance>(resources, StringComparer.Ordinal);
    }

    /// <summary>Gets runtime resources keyed by workflow resource name.</summary>
    public IReadOnlyDictionary<string, IWorkflowRuntimeResourceInstance> Resources => new ReadOnlyDictionary<string, IWorkflowRuntimeResourceInstance>(new Dictionary<string, IWorkflowRuntimeResourceInstance>(_resources, StringComparer.Ordinal));

    /// <summary>Attempts to get a runtime resource instance by workflow resource name.</summary>
    public bool TryGet(string resourceName, out IWorkflowRuntimeResourceInstance? resource)
    {
        return _resources.TryGetValue(resourceName, out resource);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        List<Exception> errors = [];
        foreach (IWorkflowRuntimeResourceInstance resource in _resources.Values)
        {
            try
            {
                await resource.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        if (errors.Count == 1)
        {
            throw errors[0];
        }

        if (errors.Count > 1)
        {
            throw new AggregateException(errors);
        }
    }
}
