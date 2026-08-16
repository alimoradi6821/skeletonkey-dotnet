# ADR 0015: Catalog-Aware Analysis and Execution Planning

Status: Accepted

The primary goal is a complete, stable, professional automation library.

Semantic validation verifies document-local language rules.

Catalog-aware analysis resolves node contracts, ports, capabilities, and resource requirements.

Execution planning converts analyzed workflow structure into immutable runtime-ready dependency metadata.

Analysis and planning do not execute workflows or nodes.

Semantic validation remains separate because workflow JSON can be structurally valid without knowing which catalog will be used. Catalog analysis is required after validation so every node is resolved by exact `type` and `typeVersion`, with no latest-version lookup, fallback, migration, or host policy.

Effective ports combine static catalog ports with deterministic dynamic ports derived from literal parameter data. Dynamic ports do not evaluate bindings, expressions, resources, or locators. This keeps authoring-time graph analysis pure and repeatable.

Parameter-schema evaluation is intentionally bounded. The default analyzer checks the accepted built-in required-property contract and reports unavailable arbitrary schema evaluation instead of pretending to implement a partial JSON Schema engine.

Resource compatibility is analyzed before runtime by matching `$resource` wrappers to workflow resource declarations, kinds, and required capabilities. No provider is contacted, no resource is acquired, and no lock is scheduled.

Planning consumes analysis instead of repeating it. A plan records deterministic steps in workflow document order, but plan order is not execution order. Dependencies are explicit and describe readiness relationships for a future runtime.

Bindings and expressions can create data-read dependencies because parameter materialization may require prior node outputs. These dependencies do not execute materialization and do not imply hidden control flow.

Loop cycles differ from invalid graph cycles. Loop `continue` and `break` back edges are structured boundary metadata and are not unrolled. Unstructured control or data cycles remain planning errors.

Invocation remains an opaque boundary. The plan preserves child workflow ID, optional exact version, stream metadata, resource mapping shape, and fixed result port without loading or analyzing the child workflow.

Plans contain no handlers, mutable runtime state, live resources, event dispatch, persistence, browser automation, dependency injection, plugin loading, or AI integration. No execution occurs in this phase.
