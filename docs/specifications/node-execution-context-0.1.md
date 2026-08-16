# Node Execution Context 0.1

`INodeExecutionContext` defines the host-neutral context passed to one node handler invocation.

It exposes:

- exact `NodeExecutionIdentity`
- runtime-owned `INodeExecutionEventWriter`
- scoped `INodeResourceAccessor`

The context does not expose `IServiceProvider`, dependency-injection containers, mutable workflow state, mutable variables, mutable execution plans, host-specific objects, backend clients, transport clients, browser objects, or arbitrary service dictionaries.

Cancellation is passed explicitly to asynchronous methods rather than stored as mutable context state.

`INodeExecutionEventWriter` lets handlers request log, progress, and streamed-output observations. A future runtime owns event IDs, root sequence numbers, timestamps, execution identity enrichment, invocation identity enrichment, workflow identity enrichment, node identity enrichment, redaction, persistence, and dispatch.

Handlers must not construct full workflow events themselves and must not call transport or backend APIs directly through this context.
