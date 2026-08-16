# Phase 0-14 Loop, Invocation, Resource, and Interaction Runtime

SkeletonKey Phase 0-14 extends the core runtime without changing host boundaries. The default runtime now keeps separate runtime activation identities from handler retry attempts so planned nodes can execute repeatedly inside loop frames.

The runtime executes `flow.foreach`, `flow.repeat`, and `flow.while` as built-in loop boundaries. Loop body activations receive `WorkflowIterationContext` values keyed by loop node ID, and `continue` or `break` connections return control to the loop controller. Sequential and nested loop paths use deterministic plan dependencies and runtime limits from `WorkflowRuntimeOptions`.

`workflow.invoke` remains runtime-owned instead of a normal node handler. Hosts supply an explicit `IWorkflowRepository`, the runtime resolves an exact `WorkflowReference`, materializes child inputs, runs the child workflow through the same validation, analysis, planning, and execution pipeline, and returns a result object from the invocation node. Stream policy contracts are preserved as invocation metadata; no external stream transport is introduced in this phase.

Runtime resource support is explicit and provider-neutral. Hosts provide `IWorkflowRuntimeResourceProvider` instances by resource kind. The runtime creates resource instances for planned node slots, exposes only slot-scoped `INodeResourceAccessor` leases to handlers, and coordinates shared or exclusive lease access without service location, plugin discovery, or browser-specific resources.

Human interaction now has a process-local session API. `IWorkflowRuntime.StartAsync` returns an `IWorkflowExecutionSession`; when an `interaction.request` node has no immediate host handler, the session exposes a pending `PendingWorkflowInteraction` and resumes when the host submits a `WorkflowInteractionContinuation`. This is in-memory only and intentionally excludes durable persistence, process-restart resume, checkpoints, databases, or distributed execution.
