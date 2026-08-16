# ADR 0013: Runtime State, Context, and Handler Boundaries

The primary goal is a complete, stable, professional automation library.

Runtime state is exposed through immutable snapshots.

Handlers execute one exact node definition contract.

Handlers receive explicit inputs, scoped resources, and runtime-owned observation interfaces.

Contracts do not perform execution.

## Decision

SkeletonKey separates runtime and handler contracts from any future runtime implementation. Phase 0-10 adds immutable execution state snapshots, node execution request contracts, scoped node execution context contracts, runtime-owned event-writing contracts, scoped resource-access contracts, and exact node handler interfaces.

Lifecycle state is distinct from terminal technical status. Lifecycle values such as `Created`, `Running`, `Suspended`, and `Completed` describe where a workflow, invocation, or node attempt is in its lifecycle. Terminal technical status remains represented by existing result contracts such as `Succeeded`, `Failed`, and `Cancelled`.

Execution state is exposed as immutable snapshots because hosts, observers, tests, and future persistence layers need stable point-in-time data. Snapshots do not mutate themselves and do not own clocks. Revisions and timestamps are supplied externally by the future runtime so contracts remain deterministic and host-neutral.

Node handlers receive materialized parameters because binding, expression, resource, and locator wrappers belong to future runtime preparation, not normal handler code. Handlers do not receive mutable workflow context, mutable variables, mutable execution plans, host containers, backend clients, or transport clients.

Control activation and data values are separate. A control edge tells a node why it is allowed to run. A data edge carries JSON values. Keeping them separate supports control-flow nodes, stream-like outputs, subworkflow invocation, and interaction nodes without conflating port activation with payload data.

Handlers return lightweight results. The future runtime owns execution IDs, timestamps, event IDs, root sequence numbers, metrics, redaction, and conversion into full `NodeExecutionResult` values. Handlers report expected failures with structured `WorkflowError` values. Unexpected exceptions are future runtime faults.

Event identity is runtime-owned. Handler event writers accept log, progress, and output observations, but handlers do not construct full workflow events and do not select event IDs, sequence numbers, timestamps, or transport behavior.

Resource access is scoped by declared node resource slots. A handler may acquire only a resource bound to its slot. Resource slots differ from acquired resource leases: a slot is a declaration and plan binding, while a lease is a scoped live runtime handle. Typed adapters are explicit and scoped to an acquired handle; they do not constitute a global service locator.

Exact handler identity is required. Handler resolution uses exact `WorkflowNodeDefinitionKey` values. There is no implicit latest-version handler resolution because version drift would make workflow behavior nondeterministic.

Retries belong to the runtime rather than handlers. `NodeExecutionIdentity.Attempt` is one-based. The first attempt is `1`; every retry gets a new attempt identity supplied by the future runtime. Handlers may inspect the attempt number, but must not increment it or schedule retries.

Durable suspension is deferred. A handler may await asynchronous work and may return success, failure, or cancellation. Workflow suspension, checkpointing, resume, and durable human interaction remain future runtime responsibilities.

## Security Boundary

Materialized secret values may reach a handler only when explicitly required by a workflow contract. Handlers must not place secrets in logs, progress messages, ordinary metadata, or output channels unless the workflow contract explicitly requires secret output. Resource adapters may contain sensitive handles. Resource handles and leases must never be serialized into workflow documents or execution events.

No secret store is implemented in this phase.

## Deferred Work

Phase 0-10 intentionally does not implement workflow execution, graph scheduling, execution-plan traversal, analyzer implementation, planner implementation, binding evaluation, expression evaluation, parameter materialization, control-flow execution, loop execution, subworkflow invocation execution, node handler implementations, resource resolution, resource creation, resource locking, locator resolution, browser automation, interaction handler implementation, retry execution, persistence, checkpointing, resume, plugin discovery, assembly scanning, dependency-injection registration, CLI, API, backend, agent, cloud, visual editor, or AI integration.
