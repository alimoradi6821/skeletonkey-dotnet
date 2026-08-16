# ADR 0006: Separate Semantic Validation Layer

## Status

Accepted for Phase 0-5.

## Context

SkeletonKey workflow JSON parsing answers whether the file is strict, well-formed SkeletonKey JSON. It does not answer whether the resulting workflow graph is meaningful according to SkeletonKey Workflow Language 0.1.

Workflows can also be constructed directly in C#, bypassing JSON entirely. Semantic checks therefore need to apply to the immutable model, not to JSON text alone.

## Decision

Semantic validation lives in `SkeletonKey.Validation`, a dedicated project that depends on `SkeletonKey.Workflow`. The workflow model does not reference validation, and `SkeletonKey.Serialization.Json` does not reference validation.

Model constructors remain permissive representation constructors. They defensively copy data, but they do not enforce semantic rules such as start-node count, connection references, reachability, identifier syntax, or execution policy duration syntax.

JSON deserialization remains parsing and shape validation only. A workflow that deserializes successfully is not necessarily semantically valid.

Validation returns deterministic issues instead of throwing for invalid workflow content. Throwing is reserved for invalid API usage, such as passing `null` to the validator.

Warnings do not invalidate workflows. They are used for non-runtime concerns or advisory graph findings, such as designer metadata problems and unreachable enabled nodes.

Validation is deterministic. Rule order is fixed, collection order follows the workflow document, and validation does not mutate source collections or JSON-backed values.

The validator does not depend on node catalogs yet. Node type availability, port availability, parameter schemas, port compatibility, and execution-specific rules require future node definition work.

Cycles are not rejected in this phase because loop and branch semantics have not been designed. The reachability check only reports enabled nodes that cannot be reached from the single valid `core.start` node.

Designer metadata issues are warnings because designer positions and sizes have no runtime semantics.

## Consequences

Semantic validation can be run after JSON parsing or after programmatic construction.

The validator is stateless and safe for concurrent use.

Future phases can add node catalog and execution-aware rules without weakening parsing, constructor immutability, or the current validation result contract.
