# Node Handler Contracts 0.1

`INodeHandler` defines a handler for one exact versioned node definition:

```csharp
public interface INodeHandler
{
    WorkflowNodeDefinitionKey Definition { get; }

    ValueTask<NodeHandlerResult> ExecuteAsync(
        NodeExecutionRequest request,
        INodeExecutionContext context,
        CancellationToken cancellationToken = default);
}
```

Handler identity uses exact `WorkflowNodeDefinitionKey` values. There is no implicit latest-version behavior.

`NodeHandlerCompletionStatus` values are:

- `Succeeded`
- `Failed`
- `Cancelled`

`Suspended` is not a handler completion result. Durable suspension and process-resume semantics are deferred to future runtime work.

`NodeHandlerOutputs` separates activated control output ports from data output port values. Control output IDs are ordered and case-sensitive, and duplicates are invalid. Data output values are defensively cloned. Future runtime validation checks outputs against the node definition.

`NodeHandlerResult` contains completion status, outputs, optional `WorkflowError`, and optional host-neutral metadata. Failed results require a structured error. Cancelled results may contain a cancellation error or no error. The result contains no execution IDs, timestamps, sequence numbers, metrics, resource handles, or leases.

Expected node failures, such as an external provider rejection or required resource unavailability, should normally return `NodeHandlerResult.Status = Failed` with a structured `WorkflowError`.

Handlers must honor the supplied cancellation token. Cancellation may return a cancelled result or throw `OperationCanceledException` associated with the supplied token. A future runtime normalizes cancellation behavior.

Handlers are expected to receive materialized JSON parameters. Handler implementations do not evaluate `$binding` or `$expression` wrappers as part of the normal contract.

Unexpected handler exceptions are future runtime faults. A future runtime will catch exceptions, redact sensitive data, create structured runtime errors, complete the node attempt as failed, and apply node error policy. This phase does not implement that behavior.

`INodeHandlerResolver` performs exact lookup only:

```csharp
bool TryResolve(
    WorkflowNodeDefinitionKey definition,
    out INodeHandler? handler);
```

The resolver contract does not define assembly scanning, plugin loading, dependency-injection adapters, mutable registration APIs, global registries, service location, or handler implementation discovery.
