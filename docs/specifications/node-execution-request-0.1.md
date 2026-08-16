# Node Execution Request 0.1

`NodeExecutionIdentity` identifies one exact node execution attempt. It contains root execution ID, invocation ID, optional parent invocation ID, workflow ID, node ID, exact `WorkflowNodeDefinitionKey`, plan ID, step ID, and one-based attempt number.

IDs are supplied by a future runtime. Comparisons are ordinal and case-sensitive. No ID generation is implemented.

`NodePortValueSet` represents ordered JSON values for one port. An empty set means no value was supplied. A set containing one `null` item represents one explicit JSON null value. JSON values are defensively cloned on input and output.

`NodePortValueMap` represents an ordered, case-sensitive map from workflow port IDs to `NodePortValueSet` values. Port multiplicity is not inferred by this contract. Future catalog-aware validation and runtime execution enforce multiplicity.

`NodeExecutionRequest` contains:

- exact node execution identity
- fully materialized handler parameters
- ordered activated control input port IDs
- data-capable input port values
- active explicit iteration contexts keyed by loop node ID

Normal handlers should receive no unresolved `$binding`, `$expression`, `$resource`, or `$locator` wrappers after future runtime materialization. This phase does not materialize parameters, evaluate bindings, evaluate expressions, resolve locators, resolve resources, or expose mutable workflow inputs, variables, or execution state.

Phase 0-11 adds materialization helpers that can produce the plain JSON object expected by `NodeExecutionRequest.Parameters`. Materialization remains separate from node execution and does not construct or execute the request automatically.
