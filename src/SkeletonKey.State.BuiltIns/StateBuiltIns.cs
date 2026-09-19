using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.State.Abstractions;

namespace SkeletonKey.State.BuiltIns;

/// <summary>Provides built-in durable state node definitions.</summary>
public static class StateBuiltInWorkflowNodeCatalog
{
    /// <summary>Gets the durable state catalog document.</summary>
    public static NodeCatalogDocument Document { get; } = new(
        id: "skeletonkey-state-builtins",
        version: "0.1.0",
        name: "SkeletonKey Durable State Built-in Nodes",
        definitions:
        [
            Definition("state.get", ["key"], "found", "value", "version", "updatedAtUtc"),
            Definition("state.put", ["key"], "version", "updatedAtUtc"),
            Definition("state.exists", ["key"], "exists", "version"),
            Definition("state.delete", ["key"], "deleted"),
            Definition("state.compareExchange", ["key"], "succeeded", "found", "value", "version", "updatedAtUtc"),
            Definition("state.leaseAcquire", ["key", "ownerId", "durationMilliseconds"], "acquired", "leaseId", "fencingToken", "acquiredAtUtc", "expiresAtUtc", "currentOwnerId", "currentFencingToken", "currentExpiresAtUtc"),
            Definition("state.leaseRenew", ["key", "leaseId", "fencingToken", "durationMilliseconds"], "succeeded", "expiresAtUtc", "currentOwnerId", "currentFencingToken", "currentExpiresAtUtc"),
            Definition("state.leaseRelease", ["key", "leaseId", "fencingToken"], "succeeded", "currentOwnerId", "currentFencingToken", "currentExpiresAtUtc"),
        ]);

    /// <summary>Gets the immutable durable state catalog.</summary>
    public static WorkflowNodeDefinitionCatalog Catalog { get; } = new(Document.Definitions);

    private static WorkflowNodeDefinition Definition(string type, string[] required, params string[] dataOutputs)
    {
        Dictionary<string, WorkflowPortDefinition> outputs = new(StringComparer.Ordinal)
        {
            ["continue"] = new("continue", WorkflowPortDirection.Output),
        };
        foreach (string output in dataOutputs)
        {
            outputs[output] = new(output, WorkflowPortDirection.Output, roles: ["data"]);
        }

        return new WorkflowNodeDefinition(
            type,
            1,
            displayName: type,
            category: "state",
            parametersSchema: new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray([.. required.Select(static value => JsonValue.Create(value))]),
            },
            inputs: new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
            {
                ["main"] = new("main", WorkflowPortDirection.Input),
            },
            outputs: outputs,
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Action),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["scope"] = "workflow",
                    ["key"] = "example/item",
                },
            ]);
    }
}

/// <summary>Creates durable state built-in handlers over an explicit state store.</summary>
public static class StateBuiltInRuntimeHandlers
{
    /// <summary>Creates the durable state handler set.</summary>
    public static IReadOnlyList<INodeHandler> Create(IWorkflowStateStore store, string hostNamespace = "default")
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostNamespace);
        List<INodeHandler> handlers =
        [
            new StateGetHandler(store, hostNamespace),
            new StatePutHandler(store, hostNamespace),
            new StateExistsHandler(store, hostNamespace),
            new StateDeleteHandler(store, hostNamespace),
            new StateCompareExchangeHandler(store, hostNamespace),
        ];
        if (store is IWorkflowLeaseStore leaseStore)
        {
            handlers.Add(new StateLeaseAcquireHandler(store, leaseStore, hostNamespace));
            handlers.Add(new StateLeaseRenewHandler(store, leaseStore, hostNamespace));
            handlers.Add(new StateLeaseReleaseHandler(store, leaseStore, hostNamespace));
        }

        return handlers.AsReadOnly();
    }
}

/// <summary>Base class for provider-neutral durable state handlers.</summary>
public abstract class StateHandlerBase : INodeHandler
{
    private readonly IWorkflowStateStore _store;
    private readonly string _hostNamespace;

    /// <summary>Initializes one exact state handler.</summary>
    protected StateHandlerBase(string type, IWorkflowStateStore store, string hostNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostNamespace);
        Definition = new WorkflowNodeDefinitionKey(type, 1);
        _store = store;
        _hostNamespace = hostNamespace;
    }

    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; }

    /// <inheritdoc />
    public async ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await ExecuteStateAsync(request, Address(request, request.Parameters), cancellationToken).ConfigureAwait(false);
        }
        catch (WorkflowStateStoreException exception)
        {
            return Failure(request, exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Failure(request, WorkflowStateErrorCodes.InvalidRequest, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Failure(request, WorkflowStateErrorCodes.InvalidRequest, exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NodeHandlerResult.Cancelled(new WorkflowError(WorkflowStateErrorCodes.OperationCancelled, "Durable state operation was cancelled.", request.Identity.NodeId));
        }
    }

    /// <summary>Executes the specific durable state operation.</summary>
    protected abstract ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken);

    /// <summary>Gets the configured store.</summary>
    protected IWorkflowStateStore Store => _store;

    /// <summary>Creates the normal continuation result.</summary>
    protected static NodeHandlerResult Main(IReadOnlyDictionary<string, NodePortValueSet> outputs)
    {
        return NodeHandlerResult.Success(new NodeHandlerOutputs(["continue"], outputs));
    }

    /// <summary>Converts one entry into common output fields.</summary>
    protected static void AddEntryOutputs(IDictionary<string, NodePortValueSet> outputs, WorkflowStateEntry? entry)
    {
        outputs["found"] = new([JsonValue.Create(entry is not null)]);
        outputs["value"] = new([entry?.Value]);
        outputs["version"] = new([entry is null ? null : JsonValue.Create(entry.Version)]);
        outputs["updatedAtUtc"] = new([entry is null ? null : JsonValue.Create(entry.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture))]);
    }

    private WorkflowStateAddress Address(NodeExecutionRequest request, JsonObject parameters)
    {
        string key = RequiredString(parameters, "key");
        string scope = OptionalString(parameters, "scope", "workflow");
        return scope switch
        {
            "workflow" => new WorkflowStateAddress(WorkflowStateScopeKind.Workflow, request.Identity.WorkflowId, key),
            "host" => new WorkflowStateAddress(WorkflowStateScopeKind.Host, _hostNamespace, key),
            "custom" => new WorkflowStateAddress(WorkflowStateScopeKind.Custom, RequiredString(parameters, "namespace"), key),
            _ => throw new InvalidOperationException("Parameter 'scope' must be workflow, host, or custom."),
        };
    }

    /// <summary>Reads a required string parameter.</summary>
    protected static string RequiredString(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new InvalidOperationException($"Parameter '{name}' must be a string.");
    }

    /// <summary>Reads an optional string parameter.</summary>
    protected static string OptionalString(JsonObject parameters, string name, string fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : fallback;
    }

    /// <summary>Reads an optional nullable string parameter.</summary>
    protected static string? OptionalNullableString(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
    }

    /// <summary>Reads a required positive integer parameter.</summary>
    protected static long RequiredPositiveLong(JsonObject parameters, string name)
    {
        if (parameters[name] is JsonValue value)
        {
            if (value.TryGetValue(out long longValue) && longValue > 0)
            {
                return longValue;
            }

            if (value.TryGetValue(out int intValue) && intValue > 0)
            {
                return intValue;
            }
        }

        throw new InvalidOperationException($"Parameter '{name}' must be a positive integer.");
    }

    /// <summary>Projects an active lease into stable workflow outputs.</summary>
    protected static void AddLeaseOutputs(IDictionary<string, NodePortValueSet> outputs, WorkflowLease? lease, string prefix = "")
    {
        outputs[prefix + "OwnerId"] = new([lease is null ? null : JsonValue.Create(lease.OwnerId)]);
        outputs[prefix + "FencingToken"] = new([lease is null ? null : JsonValue.Create(lease.FencingToken)]);
        outputs[prefix + "ExpiresAtUtc"] = new([lease is null ? null : JsonValue.Create(lease.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture))]);
    }

    private static NodeHandlerResult Failure(NodeExecutionRequest request, string code, string message)
    {
        return NodeHandlerResult.Failure(new WorkflowError(code, message, request.Identity.NodeId));
    }
}

/// <summary>Base class for state operations that mutate external durable state.</summary>
public abstract class StateSideEffectHandlerBase(string type, IWorkflowStateStore store, string hostNamespace)
    : StateHandlerBase(type, store, hostNamespace), IExternalSideEffectNodeHandler
{
}

/// <summary>Executes <c>state.get</c>.</summary>
public sealed class StateGetHandler(IWorkflowStateStore store, string hostNamespace = "default") : StateHandlerBase("state.get", store, hostNamespace)
{
    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken)
    {
        WorkflowStateEntry? entry = await Store.GetAsync(address, cancellationToken).ConfigureAwait(false);
        Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal);
        AddEntryOutputs(outputs, entry);
        return Main(outputs);
    }
}

/// <summary>Executes <c>state.put</c>.</summary>
public sealed class StatePutHandler(IWorkflowStateStore store, string hostNamespace = "default") : StateSideEffectHandlerBase("state.put", store, hostNamespace)
{
    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken)
    {
        WorkflowStateEntry entry = await Store.PutAsync(address, request.Parameters["value"]?.DeepClone(), cancellationToken).ConfigureAwait(false);
        Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal)
        {
            ["version"] = new([JsonValue.Create(entry.Version)]),
            ["updatedAtUtc"] = new([JsonValue.Create(entry.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture))]),
        };
        return Main(outputs);
    }
}

/// <summary>Executes <c>state.exists</c>.</summary>
public sealed class StateExistsHandler(IWorkflowStateStore store, string hostNamespace = "default") : StateHandlerBase("state.exists", store, hostNamespace)
{
    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken)
    {
        WorkflowStateEntry? entry = await Store.GetAsync(address, cancellationToken).ConfigureAwait(false);
        Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal)
        {
            ["exists"] = new([JsonValue.Create(entry is not null)]),
            ["version"] = new([entry is null ? null : JsonValue.Create(entry.Version)]),
        };
        return Main(outputs);
    }
}

/// <summary>Executes <c>state.delete</c>.</summary>
public sealed class StateDeleteHandler(IWorkflowStateStore store, string hostNamespace = "default") : StateSideEffectHandlerBase("state.delete", store, hostNamespace)
{
    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken)
    {
        bool deleted = await Store.DeleteAsync(address, cancellationToken).ConfigureAwait(false);
        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["deleted"] = new([JsonValue.Create(deleted)]),
        });
    }
}

/// <summary>Executes <c>state.compareExchange</c>.</summary>
public sealed class StateCompareExchangeHandler(IWorkflowStateStore store, string hostNamespace = "default") : StateSideEffectHandlerBase("state.compareExchange", store, hostNamespace)
{
    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken)
    {
        WorkflowStateCompareExchangeResult result = await Store.CompareExchangeAsync(
            address,
            OptionalNullableString(request.Parameters, "expectedVersion"),
            request.Parameters["value"]?.DeepClone(),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal)
        {
            ["succeeded"] = new([JsonValue.Create(result.Succeeded)]),
        };
        AddEntryOutputs(outputs, result.Current);
        return Main(outputs);
    }
}


/// <summary>Executes <c>state.leaseAcquire</c>.</summary>
public sealed class StateLeaseAcquireHandler(
    IWorkflowStateStore store,
    IWorkflowLeaseStore leaseStore,
    string hostNamespace = "default") : StateSideEffectHandlerBase("state.leaseAcquire", store, hostNamespace)
{
    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken)
    {
        long durationMilliseconds = RequiredPositiveLong(request.Parameters, "durationMilliseconds");
        WorkflowLeaseAcquireResult result = await leaseStore.TryAcquireLeaseAsync(
            address,
            RequiredString(request.Parameters, "ownerId"),
            TimeSpan.FromMilliseconds(durationMilliseconds),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal)
        {
            ["acquired"] = new([JsonValue.Create(result.Acquired)]),
            ["leaseId"] = new([result.Lease is null ? null : JsonValue.Create(result.Lease.LeaseId)]),
            ["fencingToken"] = new([result.Lease is null ? null : JsonValue.Create(result.Lease.FencingToken)]),
            ["acquiredAtUtc"] = new([result.Lease is null ? null : JsonValue.Create(result.Lease.AcquiredAtUtc.ToString("O", CultureInfo.InvariantCulture))]),
            ["expiresAtUtc"] = new([result.Lease is null ? null : JsonValue.Create(result.Lease.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture))]),
        };
        AddLeaseOutputs(outputs, result.Current, "current");
        return Main(outputs);
    }
}

/// <summary>Executes <c>state.leaseRenew</c>.</summary>
public sealed class StateLeaseRenewHandler(
    IWorkflowStateStore store,
    IWorkflowLeaseStore leaseStore,
    string hostNamespace = "default") : StateSideEffectHandlerBase("state.leaseRenew", store, hostNamespace)
{
    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken)
    {
        WorkflowLeaseMutationResult result = await leaseStore.RenewLeaseAsync(
            address,
            RequiredString(request.Parameters, "leaseId"),
            RequiredPositiveLong(request.Parameters, "fencingToken"),
            TimeSpan.FromMilliseconds(RequiredPositiveLong(request.Parameters, "durationMilliseconds")),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal)
        {
            ["succeeded"] = new([JsonValue.Create(result.Succeeded)]),
            ["expiresAtUtc"] = new([result.Succeeded && result.Current is not null ? JsonValue.Create(result.Current.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture)) : null]),
        };
        AddLeaseOutputs(outputs, result.Succeeded ? null : result.Current, "current");
        return Main(outputs);
    }
}

/// <summary>Executes <c>state.leaseRelease</c>.</summary>
public sealed class StateLeaseReleaseHandler(
    IWorkflowStateStore store,
    IWorkflowLeaseStore leaseStore,
    string hostNamespace = "default") : StateSideEffectHandlerBase("state.leaseRelease", store, hostNamespace)
{
    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteStateAsync(NodeExecutionRequest request, WorkflowStateAddress address, CancellationToken cancellationToken)
    {
        WorkflowLeaseMutationResult result = await leaseStore.ReleaseLeaseAsync(
            address,
            RequiredString(request.Parameters, "leaseId"),
            RequiredPositiveLong(request.Parameters, "fencingToken"),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal)
        {
            ["succeeded"] = new([JsonValue.Create(result.Succeeded)]),
        };
        AddLeaseOutputs(outputs, result.Current, "current");
        return Main(outputs);
    }
}
