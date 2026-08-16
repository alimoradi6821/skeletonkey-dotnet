# Runtime Loop, Invocation, Resource, and Interaction 0.1

This specification describes Phase 0-14 runtime execution contracts.

Runtime activations:
- A runtime activation is distinct from a handler retry attempt.
- Repeated loop body executions receive increasing activation ordinals.
- Activation limits are enforced by `WorkflowRuntimeOptions`.

Loop execution:
- `flow.foreach` iterates over materialized JSON array items.
- `flow.repeat` iterates a non-negative integer count.
- `flow.while` evaluates a materialized boolean condition and is bounded by the configured loop iteration limit.
- Loop bodies may return `continue`, `break`, or terminal flow.
- Iteration context is exposed through `NodeExecutionRequest.Iterations`.

Workflow invocation:
- `workflow.invoke` requires an explicit host-supplied `IWorkflowRepository`.
- Child workflows execute through validation, analysis, planning, and runtime execution.
- Child input mapping is materialized before invocation.
- Child failure propagates as a parent invocation failure.
- Invocation stream policies remain host-neutral contracts in this phase.

Runtime resources:
- Runtime resource providers are explicit constructor inputs.
- Providers are selected by exact resource kind.
- Node handlers access resources only through declared resource slots.
- Leases are scoped to handler execution and support shared or exclusive access.
- Resource instances are not serialized into workflow documents, outputs, or events.

Interaction continuations:
- `StartAsync` creates an in-memory execution session.
- `interaction.request` may suspend when no immediate interaction handler is registered.
- Pending interactions are exposed as session-owned continuation contracts.
- Continuation identifiers are validated by the owning session.
- Session cancellation completes pending interactions with cancelled responses.

Out of scope:
- Durable persistence, checkpoints, databases, process-restart resume, retry execution, parallel scheduling, distributed execution, plugin discovery, dependency injection, locator resolution, file or HTTP nodes, Playwright, Selenium, Puppeteer, FlaUI, browser resources, browser handlers, CLI/API/backend surfaces, cloud services, visual editors, AI behavior, and legacy Python compatibility.
