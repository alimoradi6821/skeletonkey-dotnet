# Runtime Events 0.1

Runtime events use the existing `IWorkflowEventSink` boundary. Phase 0-13 adds `RuntimeWorkflowEvent` for runtime-owned state and handler observation events.

Runtime event sequence numbers are monotonic and one-based per execution. The runtime owns event IDs, timestamps, execution identity, invocation identity, node enrichment, and payload cloning.

Handlers write observations through `INodeExecutionEventWriter`. Handler requests become runtime-sequenced events; handlers cannot choose event IDs, sequence numbers, or timestamps.

The runtime emits events for execution creation, readiness, start, node readiness, node start, node completion, node failure, node cancellation, skipped nodes, execution completion, execution failure, execution cancellation, handler logs, handler progress, and handler output observations.
