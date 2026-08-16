# ADR 0014: Workflow Value Resolution and Safe Evaluation

The primary goal is a complete, stable, professional automation library.

Workflow values are materialized before node handler invocation.

Bindings resolve explicit workflow data references.

Expressions perform deterministic, side-effect-free computation over workflow data.

Resource and locator references remain provider-owned contracts rather than JSON values.

## Decision

SkeletonKey separates value resolution from workflow execution. Phase 0-11 adds binding resolution, read-only JSON Pointer resolution, safe expression evaluation, recursive workflow-value materialization, and node parameter materialization without adding a workflow runtime.

Handlers receive materialized parameters because handler code should not evaluate `$binding` or `$expression` wrappers. Future runtimes prepare `NodeExecutionRequest.Parameters` before invoking a handler.

Bindings and expressions are evaluated before handler invocation. Bindings resolve explicit workflow input, variable, node output, and iteration references. Expressions perform pure computation over those resolved data roots.

`$literal` prevents recursive interpretation. Its inner value is returned as literal JSON, so reserved wrapper names can be used as application data.

Binding defaults remain literal. Defaults are defensively cloned and are not recursively materialized, so a default containing `$binding` or `$expression` remains ordinary JSON.

Node multi-values project deterministically. Zero node output values are missing, one value is that JSON value including explicit null, and multiple values become an ordered JSON array.

Missing and explicit null are distinct. Missing data follows binding `onMissing`; explicit JSON null is a successful JSON value.

Resource and locator references are not JSON-materialized. Resources must be consumed through catalog resource slots and future runtime resource binding. Locators require future locator-aware preparation or provider resolution.

Expressions are pure, culture-invariant, and side-effect free. They have no I/O, host access, reflection, dynamic invocation, current time, randomness, resource access, locator access, user-defined functions, or arbitrary function registry.

Implicit coercion is rejected. Numeric operations require numbers, logical operations require booleans, and string concatenation requires two strings. Explicit conversion functions provide limited documented conversions.

Short-circuit semantics matter because unselected logical, null-coalescing, conditional, and `coalesce` branches must not produce errors.

Operation and result limits exist to prevent pathological nested expressions or unbounded JSON/string growth without relying on machine memory or timeouts.

No workflow runtime is added in this phase.

## Deferred Work

Phase 0-11 intentionally defers workflow execution, plan traversal, graph scheduling, node execution, handler execution, analyzer implementation, planner implementation, resource resolution, locator resolution, browser automation, human interaction execution, persistence, checkpointing, plugin loading, dependency-injection registration, CLI, API, backend, agent, cloud, visual editor, and AI integration.
