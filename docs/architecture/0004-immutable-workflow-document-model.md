# ADR 0004: Immutable Workflow Document Model

## Status

Accepted for Phase 0-3.

## Context

SkeletonKey workflows are graph documents that later phases will serialize, validate, display, and execute. The document model must therefore be safe to share across readers and must not embed runtime behavior.

## Decision

Workflow document models are immutable after construction. Constructors defensively copy incoming collections so later caller mutations do not change the constructed model.

Node parameters use `JsonObject` because each node type will eventually define its own parameter shape. Workflow variables and input defaults use `JsonNode` because they may contain arbitrary JSON values without requiring execution-time `object` or `dynamic` data.

JSON-backed properties return defensive deep clones. This keeps callers from mutating internal model state through mutable `JsonNode` references.

Document models contain no execution state such as status, result, errors, timing, or attempts. Those concepts belong to future execution models.

Designer metadata is isolated from runtime semantics. Positions and sizes can support authoring surfaces without affecting traversal or execution.

Constructors do not validate semantic rules such as node ID syntax, required start nodes, graph reachability, type versions, or port compatibility. Those rules belong to the future validator so the model remains a representation of the document rather than a policy engine.

The model remains independent from the future JSON serializer. It uses C# domain types and JSON node values where extensibility is required, but it does not define serialization behavior in this phase.

## Consequences

The model is safe for read-only concurrent use after construction. JSON-backed properties allocate clones when accessed, which is an intentional tradeoff for defensive immutability at this foundation stage.
