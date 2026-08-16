# Workflow Human Interaction 0.1

SkeletonKey defines host-neutral human interaction contracts in `SkeletonKey.Abstractions`. Interaction kinds are confirmation, text, secret, choice, multiple choice, and manual action.

`WorkflowInteractionRequest` carries request, execution, invocation, workflow, and node identity, plus materialized prompt text, optional description, ordered options, required/default information, timeout, and metadata. JSON values are defensively cloned. Secret requests are sensitive and must not declare defaults.

`WorkflowInteractionResponse` carries request ID, status, optional value, and timestamp. Response statuses are submitted, cancelled, timed out, and unavailable. Explicit JSON null remains distinguishable from no value.

`IWorkflowInteractionHandler` defines the future host boundary:

```csharp
ValueTask<WorkflowInteractionResponse> RequestAsync(
    WorkflowInteractionRequest request,
    CancellationToken cancellationToken = default);
```

The reserved `interaction.request` node uses `typeVersion` 1 and parameters `kind`, `prompt`, `description`, `options`, `default`, `required`, and `timeout`. It has input port `main` and output port `result`. Prompt and description may be string literals, bindings, or expressions, but they are not evaluated in this phase.

Future runtimes may suspend a workflow invocation while awaiting a response. Timeout produces `TimedOut`, host unavailability produces `Unavailable`, and user cancellation produces `Cancelled`. Suspension, resumption, handlers, UI, transport, persistence, and execution are intentionally deferred.

`interaction.request` is a node definition contract, not a node handler implementation. Future interaction node handlers use the same exact handler identity, materialized request, scoped resource access, cancellation, and runtime-owned event-writing boundaries as other node handlers.
