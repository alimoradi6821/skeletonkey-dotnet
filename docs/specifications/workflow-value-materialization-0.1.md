# Workflow Value Materialization 0.1

`WorkflowValueMaterializer` recursively processes workflow-value JSON.

Scalars return defensive clones. Arrays are materialized item by item in order. Ordinary objects are materialized property by property in insertion order.

Reserved wrappers:

- `$literal`: unwraps and returns its inner JSON without recursive materialization.
- `$binding`: parses with `WorkflowBindingReader` and resolves with `WorkflowBindingResolver`.
- `$expression`: parses with `WorkflowExpressionReader` and evaluates with `WorkflowExpressionEvaluator`.
- `$resource`: fails with `SKV1017`.
- `$locator`: fails with `SKV1018`.

Malformed reserved wrappers fail with `SKV1001`. A wrapper object must contain exactly one reserved wrapper property. `$literal` is the only escape mechanism for reserved wrapper names.

Resource references are not converted to live resources. Locator references are not converted to browser or provider objects.

Materialization enforces deterministic depth, collection, string, and result limits. It does not mutate source JSON and returns defensively owned JSON.

The materializer is stateless, deterministic, thread-safe, and performs no workflow execution, node execution, handler execution, resource resolution, locator resolution, I/O, host access, browser automation, or persistence.
