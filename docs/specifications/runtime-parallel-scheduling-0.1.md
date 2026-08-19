# Runtime Parallel Scheduling 0.1

## Ready-step admission

The scheduler orders ready steps by execution-plan document order. If the first ready step is a resource-free, non-suspending, non-terminal `Control` or `Action` step, the scheduler admits the contiguous eligible prefix up to `MaximumParallelSteps`. Otherwise it executes only the first ready step.

The runtime must await every admitted step before computing the next ready set. Dependencies therefore never observe a partially completed batch.

## Deterministic projection

Event publication is serialized. Event sequence and sink observation order are identical and monotonic. Runtime results and node snapshots are ordered by plan position and then activation attempt; handler completion timing does not define public collection order.

If more than one concurrently admitted node fails without handling, the failure belonging to the earliest plan step becomes the workflow error.

## Parallel foreach

A `flow.foreach` node requests parallel execution with:

```json
{
  "items": [],
  "execution": {
    "mode": "parallel",
    "maxConcurrency": 4
  }
}
```

The effective concurrency is `min(execution.maxConcurrency, MaximumParallelSteps)`. Parallel admission applies when every body edge reaches one resource-free, non-terminal handler step whose control edges return directly to the owning loop. Other body shapes execute sequentially without changing workflow meaning.

Iterations are admitted in source order in bounded batches. A break or terminal result prevents later batches from starting. Cancellation is propagated to every admitted handler.

## Durable and resource boundaries

When a checkpoint store is present, root scheduling and foreach iteration scheduling are sequential. This retains checkpoint 0.2's single safe frontier and explicit handling of interrupted running nodes.

Steps with runtime resource requirements are serialized because provider instances do not declare a concurrency model. Loop orchestration, workflow invocation, human interaction, and terminal steps are serialized.

Distributed workers, leases, fairness across executions, parallel durable checkpoints, multi-step parallel loop frames, and parallel resource access are outside version 0.1.
