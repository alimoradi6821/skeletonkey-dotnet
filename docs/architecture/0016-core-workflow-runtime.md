# 0016: Core Workflow Runtime

The primary goal is a complete, stable, professional automation library.

Phase 0-13 introduces the first workflow runtime that actually executes Workflow documents. The runtime executes immutable analyzed plans. Node handlers execute exact node-definition contracts. The runtime owns scheduling, state, identity, event ordering, parameter preparation, and result aggregation. Phase 0-13 executes core non-loop workflows but does not yet execute loops, subworkflows, resources, locators, or browser automation.

Execution consumes semantic validation, catalog-aware analysis, and an execution plan before any handler is called. This keeps runtime behavior aligned with static diagnostics and prevents a second raw-graph interpreter from developing inside the execution engine.

The runtime does not directly reinterpret raw graph semantics when a plan exists. The plan is the contract between analysis/planning and execution: it contains step identity, dependencies, entry steps, terminal steps, boundaries, resource-use declarations, and exact definition keys.

Initial scheduling is deterministic and sequential. Independent ready steps may exist, but Phase 0-13 chooses the next ready step by plan document order and executes it in process. This makes event order, node result order, and test behavior stable while leaving parallel scheduling for a later phase.

Plan order differs from readiness. Plan order is only a tie breaker among ready steps; dependency readiness still controls whether a step can run. Data-only dependencies do not activate control-flow nodes, and branch targets only run when their control input is activated.

Handlers receive materialized parameters. The runtime builds a value-resolution context from workflow inputs, variables, completed prior-node outputs, and current iteration contexts, then uses the node parameter materializer before calling a handler. Handlers do not evaluate binding or expression syntax themselves.

Control and data outputs propagate separately. Control outputs activate target control inputs through control dependencies. Data outputs are validated against data-capable effective ports, stored as ordered port value sets, and made available for explicit data dependencies plus later binding and expression materialization.

The runtime owns identity, timestamps, sequence, and metrics. Execution IDs and plan IDs are caller supplied. The runtime derives deterministic invocation and node-attempt identities, obtains timestamps from a clock abstraction, assigns one-based event sequences, and aggregates final metrics.

Handler failures differ from runtime faults. Expected handler failures return structured handler results. Unexpected exceptions are caught at the runtime boundary and normalized to stable runtime errors without exposing stack traces in normal workflow outputs.

Cancellation differs from failure. Cancellation is observed before execution, between steps, during event publication, and during handlers. Cancellation returns a cancelled technical status and does not become an ordinary failed workflow.

Loops and invocation remain unsupported because iteration state, subworkflow loading, child invocation ownership, and continuation behavior need their own phase. Reachable loop and subworkflow boundaries fail explicitly with an unsupported-boundary runtime error instead of being skipped or faked.

No persistence exists in this phase. The state store is in-memory only and provides immutable snapshots, revisions, timestamps, and legal transition enforcement without checkpointing, resume, or distributed execution.

Unsupported reachable boundaries fail explicitly so authors get deterministic feedback at the first executable boundary the runtime cannot honor. Unreachable unsupported nodes on a completed branch may be skipped.

This is the first phase that executes Workflows end to end: start, return, if, switch, parameter materialization, output propagation, ordered runtime events, cancellation, and representative branch workflows are now executable.
