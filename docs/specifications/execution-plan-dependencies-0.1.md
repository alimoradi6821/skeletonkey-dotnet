# Execution Plan Dependencies 0.1

Execution-plan dependencies are immutable metadata between planned steps.

Control dependencies come from valid control-compatible workflow connections. They preserve source step, target step, source port, target port, and connection path.

Data dependencies come from:

- valid data-compatible workflow connections
- `$binding` wrappers with `source = node`
- `$expression` wrappers with `nodes['node-id'].outputs['port-id']` references

Bindings and expressions inside `$literal` are ignored. Input, variable, and iteration references do not create inter-node data dependencies.

Equivalent dependencies are deduplicated by source step, target step, kind, source port, and target port. Data dependencies do not evaluate values and do not create hidden control transitions.

Unstructured dependency cycles block planning. Structured loop back edges to loop `continue` or `break` inputs are preserved as loop metadata and are not rejected as ordinary cycles.
