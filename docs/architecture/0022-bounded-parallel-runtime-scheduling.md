# Phase 0-19 Bounded Parallel Runtime Scheduling

Status: implemented; repository verification required before release tagging.

The default runtime now selects a deterministic, plan-ordered batch from simultaneously ready handler steps and executes that batch concurrently. `WorkflowRuntimeOptions.MaximumParallelSteps` is the host-owned upper bound and defaults to four. Terminal, suspending, interaction, invocation, loop-orchestration, and resource-consuming steps remain serialized.

Runtime events are published through one ordered coordinator so sequence allocation and sink delivery remain monotonic even when handlers overlap. Final node results and snapshots are projected in plan order and then attempt order, independent of handler completion timing. Failure selection among concurrent unhandled failures uses the earliest plan position.

`flow.foreach` now honors `execution.mode: parallel` and `execution.maxConcurrency` for eligible single-handler loop bodies that return directly to the loop. The effective concurrency is the smaller of the workflow declaration and the runtime-wide limit. Work is admitted in bounded batches; a break or terminal signal stops admission of later batches while already admitted iterations finish cooperatively.

Checkpoint format 0.2 cannot represent a resumable parallel frontier. Executions configured with a checkpoint store therefore retain sequential scheduling, preserving the existing safe-boundary and explicit-recovery guarantees. Multi-step parallel foreach bodies also retain sequential behavior until iteration-local durable state and aggregation contracts are versioned.

This phase does not add distributed scheduling, worker leases, resource-instance concurrency, parallel child invocation, parallel suspended interactions, speculative execution, or live resource recovery.
