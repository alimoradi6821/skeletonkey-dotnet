# Workflow Binding Resolution 0.1

`WorkflowBindingResolver` resolves structured `WorkflowBinding` contracts against `WorkflowValueResolutionContext`.

Supported sources:

- `input`: `context.Inputs[binding.Name]`
- `variable`: `context.Variables[binding.Name]`
- `node`: `context.Nodes[binding.Node].Values[binding.Port]`
- `iteration`: `context.Iterations[binding.Iteration]`

After resolving a source value, the binding JSON Pointer is applied with `JsonPointerResolver`.

Missing behavior:

- `error`: missing source, missing port, absent iteration property, or missing pointer target returns a structured error.
- `null`: missing source, missing port, or missing pointer target resolves successfully to explicit JSON null.
- `default`: missing values resolve to the explicit default JSON value.

Defaults are literal JSON. Wrappers inside defaults are not evaluated or materialized. Explicit JSON null defaults are preserved.

Malformed JSON Pointers always fail and are not hidden by `onMissing`.

Type mismatch during pointer traversal is treated as a missing pointer target. Invalid pointer syntax is a syntax error.

The resolver is deterministic, stateless, thread-safe, case-sensitive, and performs no resource resolution, locator resolution, binding evaluation beyond the binding contract, workflow execution, or node execution.
