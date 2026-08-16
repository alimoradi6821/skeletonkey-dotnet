# Workflow Runtime 0.1

The runtime accepts a `WorkflowExecutionRequest` containing an immutable `WorkflowDocument`, caller-supplied execution ID, caller-supplied plan ID, cloned inputs, cloned variables, and an event sink or explicit no-op sink.

Execution stages are:

1. Semantic validation.
2. Catalog-aware analysis.
3. Execution planning.
4. Runtime state initialization.
5. Entry-step activation.
6. Dependency readiness.
7. Parameter materialization.
8. Exact handler resolution.
9. Handler execution.
10. Output validation and propagation.
11. Runtime state, result, and event update.
12. Terminal completion.

The runtime does not mutate workflow documents, request inputs, or request variables. It does not provide browser automation, resource resolution, locator resolution, loops, subworkflow invocation, persistence, resume, dependency-injection registration, assembly scanning, plugin discovery, or retry execution.

Runtime error codes use the `SKR` prefix. Phase 0-13 defines errors for validation, analysis, planning, missing handlers, identity mismatch, materialization failure, unexpected exceptions, invalid outputs, unavailable dependencies, execution limits, cancellation, unsupported boundaries, invalid state transitions, and no-progress detection.
