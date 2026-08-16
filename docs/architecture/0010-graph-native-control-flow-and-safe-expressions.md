# ADR 0010: Graph-Native Control Flow And Safe Expressions

## Status

Accepted for workflow language `0.1.0`.

## Context

The primary goal is a complete, stable, professional automation library.

Control flow is represented by the workflow graph, not nested action collections.

Expressions are deterministic, side-effect-free computations over workflow data.

Legacy behavior informs requirements, but legacy syntax does not define the language.

## Decision

SkeletonKey reserves graph-native control nodes: `flow.if`, `flow.switch`, `flow.foreach`, `flow.repeat`, `flow.while`, and `core.return`.

Branch and loop bodies are ordinary graph nodes connected through explicit ports. Nested action arrays are rejected because they create a second workflow language inside node parameters, obscure data flow, and make editor, validator, and runtime behavior harder to reason about.

Expressions are pure and deterministic. They may inspect `inputs`, `variables`, `nodes`, and `iterations`, but they cannot access browser state, host state, files, network, secrets, current time, random values, reflection, arbitrary C#, JavaScript, or `eval`.

Structured bindings remain preferable for simple references. Expressions complement bindings when a condition or lightweight deterministic transformation is needed. Complex data shaping remains a future data-node concern rather than expression object or array literals.

Iteration references use explicit loop node IDs so nested loops remain clear. Loop cycles are graph connections back to `continue`; early loop exits are connections to `break`.

`flow.while` declares `maxIterations` as a safety boundary. Reaching the boundary is future runtime behavior, not a semantic validation result in this phase.

Parallel foreach is modeled as a contract before runtime scheduling exists so tools can author and validate intent without implying execution.

Early return records an outcome contract and terminal graph behavior. It does not directly define workflow outputs.

Execution-order validation, dominance analysis, branch convergence, loop correctness, and catalog-aware port availability are deferred to future execution planning.

AI convenience does not control the language design. Humans, visual editors, external tools, and future AI tooling consume the same official contracts.
