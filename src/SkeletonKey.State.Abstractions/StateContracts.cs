using System.Text.Json.Nodes;

namespace SkeletonKey.State.Abstractions;

/// <summary>Identifies the durable namespace scope for one state address.</summary>
public enum WorkflowStateScopeKind
{
    /// <summary>State isolated by workflow identity.</summary>
    Workflow,

    /// <summary>State shared by workflows hosted in one configured host namespace.</summary>
    Host,

    /// <summary>State isolated by an explicit consumer-defined namespace.</summary>
    Custom,
}

/// <summary>Stable error codes produced by durable state providers.</summary>
public static class WorkflowStateErrorCodes
{
    /// <summary>The state request is invalid.</summary>
    public const string InvalidRequest = "SKS1001";

    /// <summary>The state provider failed.</summary>
    public const string StoreFailure = "SKS1002";

    /// <summary>The state provider remained busy beyond its configured bound.</summary>
    public const string StoreBusy = "SKS1003";

    /// <summary>The state operation was cancelled.</summary>
    public const string OperationCancelled = "SKS1004";

    /// <summary>The durable store schema is newer or otherwise unsupported.</summary>
    public const string UnsupportedSchema = "SKS1005";
}

/// <summary>Represents an expected durable state provider failure.</summary>
public sealed class WorkflowStateStoreException : Exception
{
    /// <summary>Initializes a state provider failure.</summary>
    public WorkflowStateStoreException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>Gets the stable provider error code.</summary>
    public string Code { get; }
}

/// <summary>Identifies one durable state key without encoding an application domain.</summary>
public sealed class WorkflowStateAddress : IEquatable<WorkflowStateAddress>
{
    /// <summary>Initializes a durable state address.</summary>
    public WorkflowStateAddress(WorkflowStateScopeKind scope, string @namespace, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Scope = scope;
        Namespace = @namespace;
        Key = key;
    }

    /// <summary>Gets the scope kind.</summary>
    public WorkflowStateScopeKind Scope { get; }

    /// <summary>Gets the stable namespace inside the scope.</summary>
    public string Namespace { get; }

    /// <summary>Gets the ordinal, case-sensitive key.</summary>
    public string Key { get; }

    /// <inheritdoc />
    public bool Equals(WorkflowStateAddress? other)
    {
        return other is not null && Scope == other.Scope &&
            string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) &&
            string.Equals(Key, other.Key, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as WorkflowStateAddress);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Scope, StringComparer.Ordinal.GetHashCode(Namespace), StringComparer.Ordinal.GetHashCode(Key));
}

/// <summary>Represents one durable state entry and its optimistic-concurrency token.</summary>
public sealed class WorkflowStateEntry
{
    private readonly JsonNode? _value;

    /// <summary>Initializes a durable state entry.</summary>
    public WorkflowStateEntry(JsonNode? value, string version, DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        _value = value?.DeepClone();
        Version = version;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>Gets a defensive copy of the stored JSON value.</summary>
    public JsonNode? Value => _value?.DeepClone();

    /// <summary>Gets the opaque optimistic-concurrency token.</summary>
    public string Version { get; }

    /// <summary>Gets the UTC update timestamp recorded by the provider.</summary>
    public DateTimeOffset UpdatedAtUtc { get; }
}

/// <summary>Represents the result of one atomic compare/exchange operation.</summary>
public sealed class WorkflowStateCompareExchangeResult
{
    /// <summary>Initializes a compare/exchange result.</summary>
    public WorkflowStateCompareExchangeResult(bool succeeded, WorkflowStateEntry? current)
    {
        Succeeded = succeeded;
        Current = current is null ? null : new WorkflowStateEntry(current.Value, current.Version, current.UpdatedAtUtc);
    }

    /// <summary>Gets whether the requested exchange won atomically.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the resulting entry on success or the observed entry on failure.</summary>
    public WorkflowStateEntry? Current { get; }
}

/// <summary>Defines a durable key/value store shared across independent workflow executions.</summary>
public interface IWorkflowStateStore
{
    /// <summary>Gets one entry or <see langword="null" /> when the address is absent.</summary>
    public ValueTask<WorkflowStateEntry?> GetAsync(WorkflowStateAddress address, CancellationToken cancellationToken = default);

    /// <summary>Unconditionally creates or replaces one entry.</summary>
    public ValueTask<WorkflowStateEntry> PutAsync(WorkflowStateAddress address, JsonNode? value, CancellationToken cancellationToken = default);

    /// <summary>Deletes one entry and reports whether it existed.</summary>
    public ValueTask<bool> DeleteAsync(WorkflowStateAddress address, CancellationToken cancellationToken = default);

    /// <summary>Atomically creates when <paramref name="expectedVersion" /> is null, or replaces only when the current version matches.</summary>
    public ValueTask<WorkflowStateCompareExchangeResult> CompareExchangeAsync(
        WorkflowStateAddress address,
        string? expectedVersion,
        JsonNode? value,
        CancellationToken cancellationToken = default);
}
