# Workflow Iteration 0.1

Iteration is represented by graph-native loop nodes and explicit iteration IDs. Loop node IDs are iteration scope IDs.

## flow.foreach

`flow.foreach` requires `items`, a workflow value expected to resolve to an array in a future runtime.

Optional `execution` declares `mode` and `maxConcurrency`. Modes are `sequential` and `parallel`. Sequential mode must not declare `maxConcurrency`. Parallel mode requires `maxConcurrency >= 1`.

Inputs: `main`, `continue`, `break`.

Outputs: `body`, `completed`.

## flow.repeat

`flow.repeat` requires `count`. Count may be a non-negative integer literal, binding, or expression.

Inputs: `main`, `continue`, `break`.

Outputs: `body`, `completed`.

## flow.while

`flow.while` requires `condition` and optionally declares `maxIterations`, defaulting conceptually to `1000`. The maximum is a safety boundary for a future runtime.

Inputs: `main`, `continue`, `break`.

Outputs: `body`, `completed`.

## Iteration Context

`WorkflowIterationContext` is host-neutral and immutable. `Index` is zero-based, `Number` is one-based, `Item` is defensively cloned, `HasItem` distinguishes absence from explicit JSON null, and `Count` may be absent.

## Iteration Bindings

```json
{
  "$binding": {
    "source": "iteration",
    "iteration": "each-contact",
    "path": "/item/name"
  }
}
```

Iteration bindings require `iteration` and forbid `name`, `node`, and `port`.

## Deferred Validation

The validator does not prove loop scope dominance, complete loop cycles, executable cycles, branch convergence, runtime array typing, termination, or parallel body safety.

## Planning

The default execution planner records loop boundary metadata and preserves structured loop back edges without unrolling iterations or evaluating loop counts, items, or conditions.
