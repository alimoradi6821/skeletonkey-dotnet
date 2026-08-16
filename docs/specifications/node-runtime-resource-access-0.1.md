# Node Runtime Resource Access 0.1

Node runtime resource access is scoped by declared node resource slots.

`NodeResourceBinding` records the binding produced by future planning or runtime work:

- node resource slot name
- workflow resource declaration name
- resource kind
- access mode
- ordered required capabilities
- required or optional binding flag

A resource slot is a declaration and binding. It is not a live resource.

`INodeResourceAccessor` exposes planned bindings and slot-based acquisition:

```csharp
IReadOnlyList<NodeResourceBinding> Bindings { get; }

bool TryGetBinding(
    string slotName,
    out NodeResourceBinding? binding);

ValueTask<INodeResourceLease> AcquireAsync(
    string slotName,
    CancellationToken cancellationToken = default);
```

Handlers cannot acquire resources by arbitrary workflow resource name. Missing optional resources may be represented by a future unavailable result or exception contract. No resolution, creation, locking, pooling, or retry implementation exists in this phase.

`INodeResourceLease` is asynchronously disposable and exposes one scoped `INodeResourceHandle` plus the granted access mode. Lease lifetime belongs to the handler call and future runtime.

`INodeResourceHandle` exposes host-neutral resource metadata and explicit typed adapter access:

```csharp
bool TryGetAdapter<TAdapter>(
    out TAdapter? adapter)
    where TAdapter : class;

TAdapter GetRequiredAdapter<TAdapter>()
    where TAdapter : class;
```

Adapter access is explicit and scoped to the acquired resource handle. It is not a global service locator. Resource handles and leases must never be serialized into workflow documents or execution events.

Generic workflow-value and node-parameter materialization do not resolve `$resource` wrappers. Resource references require specialized future runtime preparation through declared resource slots and scoped leases.
