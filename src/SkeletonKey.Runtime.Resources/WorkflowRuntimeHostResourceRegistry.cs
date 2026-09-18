using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Process-local registry that owns and health-checks host-lifetime runtime resources.
/// </summary>
public sealed class WorkflowRuntimeHostResourceRegistry : IWorkflowRuntimeHostResourceRegistry
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<HostResourceKey, Entry> _entries = [];
    private bool _disposed;

    /// <inheritdoc />
    public async ValueTask<IWorkflowRuntimeResourceInstance> GetOrCreateAsync(
        WorkflowRuntimeResourceRequest request,
        IWorkflowRuntimeResourceProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(provider);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Definition.Lifetime != WorkflowResourceLifetime.Host)
        {
            throw new ArgumentException("Host resource registry only accepts resources declared with host lifetime.", nameof(request));
        }

        if (!string.Equals(provider.Kind, request.Definition.Kind, StringComparison.Ordinal))
        {
            throw new ArgumentException("Runtime resource provider kind does not match the resource declaration.", nameof(provider));
        }

        HostResourceKey key = new(request.WorkflowId, request.ResourceName);
        string fingerprint = ComputeDefinitionFingerprint(request.Definition);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_entries.TryGetValue(key, out Entry? existing))
            {
                bool reusable = string.Equals(existing.DefinitionFingerprint, fingerprint, StringComparison.Ordinal);
                if (reusable)
                {
                    WorkflowRuntimeResourceHealthState health = await ReadHealthAsync(existing.Instance, cancellationToken).ConfigureAwait(false);
                    reusable = health is WorkflowRuntimeResourceHealthState.Healthy or WorkflowRuntimeResourceHealthState.Degraded;
                }

                if (reusable)
                {
                    return existing.Instance;
                }

                _entries.Remove(key);
                await existing.Instance.DisposeAsync().ConfigureAwait(false);
            }

            IWorkflowRuntimeResourceInstance instance = await provider.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            try
            {
                ValidateInstance(request, instance);
            }
            catch
            {
                await instance.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            _entries.Add(key, new Entry(fingerprint, instance));
            return instance;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> RecycleAsync(
        string workflowId,
        string resourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            HostResourceKey key = new(workflowId, resourceName);
            if (!_entries.Remove(key, out Entry? entry))
            {
                return false;
            }

            await entry.Instance.DisposeAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Entry[] entries;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            entries = [.. _entries.Values];
            _entries.Clear();
        }
        finally
        {
            _gate.Release();
        }

        List<Exception> errors = [];
        foreach (Entry entry in entries.Reverse())
        {
            try
            {
                await entry.Instance.DisposeAsync().ConfigureAwait(false);
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

    private static async ValueTask<WorkflowRuntimeResourceHealthState> ReadHealthAsync(
        IWorkflowRuntimeResourceInstance instance,
        CancellationToken cancellationToken)
    {
        if (instance is not IWorkflowRuntimeResourceHealthParticipant participant)
        {
            return WorkflowRuntimeResourceHealthState.Healthy;
        }

        try
        {
            return await participant.GetHealthAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return WorkflowRuntimeResourceHealthState.Unrecoverable;
        }
    }

    private static void ValidateInstance(
        WorkflowRuntimeResourceRequest request,
        IWorkflowRuntimeResourceInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!string.Equals(instance.ResourceName, request.ResourceName, StringComparison.Ordinal) ||
            !string.Equals(instance.Kind, request.Definition.Kind, StringComparison.Ordinal) ||
            instance.Access != request.Definition.Access ||
            string.IsNullOrWhiteSpace(instance.InstanceId))
        {
            throw new InvalidOperationException("Runtime resource provider returned an instance with incompatible identity or access.");
        }

        foreach (string capability in request.Definition.Capabilities)
        {
            if (!instance.Capabilities.Contains(capability, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Runtime resource provider returned an instance missing a required capability.");
            }
        }
    }

    private static string ComputeDefinitionFingerprint(WorkflowResourceDefinition definition)
    {
        StringBuilder canonical = new();
        canonical.Append(definition.Kind).Append('\n');
        canonical.Append((int)definition.Lifetime).Append('\n');
        canonical.Append((int)definition.Access).Append('\n');
        canonical.Append(definition.Required ? '1' : '0').Append('\n');
        foreach (string capability in definition.Capabilities)
        {
            canonical.Append(capability).Append('\n');
        }

        canonical.Append(Canonicalize(definition.Constraints)?.ToJsonString() ?? "null");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static JsonNode? Canonicalize(JsonNode? value)
    {
        if (value is JsonObject sourceObject)
        {
            JsonObject result = [];
            foreach (KeyValuePair<string, JsonNode?> property in sourceObject.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                result[property.Key] = Canonicalize(property.Value);
            }

            return result;
        }

        if (value is JsonArray sourceArray)
        {
            JsonArray result = [];
            foreach (JsonNode? item in sourceArray)
            {
                result.Add(Canonicalize(item));
            }

            return result;
        }

        return value?.DeepClone();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private readonly record struct HostResourceKey(string WorkflowId, string ResourceName);

    private sealed record Entry(string DefinitionFingerprint, IWorkflowRuntimeResourceInstance Instance);
}
