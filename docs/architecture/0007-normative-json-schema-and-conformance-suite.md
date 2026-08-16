# ADR 0007: Normative JSON Schema and Conformance Suite

## Status

Accepted for Phase 0-6.

## Context

SkeletonKey Workflow Language 0.1 now has an immutable model, strict JSON serialization, and deterministic semantic validation. Future runtimes and tools need a machine-readable JSON contract and reusable fixtures that explain which layer owns each validation responsibility.

## Decision

Workflow Language 0.1 has one normative JSON Schema at `schemas/workflow/0.1/schema.json`.

The schema is hand-authored and reviewed as source code. Schema generation from C# models is not used because the JSON contract is a language artifact, not an implementation side effect. C# model generation from the schema is also deferred.

JSON Schema validation remains separate from strict deserialization. The strict serializer owns duplicate property detection, canonical serialization, parser options, unknown property rejection, enum conversion, and deserialization into immutable models.

Semantic validation remains separate from both parsing and schema validation. Cross-document rules such as unique node IDs, start-node rules, graph references, reachability, retry delay relationships, and designer references remain in `SkeletonKey.Validation`.

`JsonSchema.Net` is used only by `SkeletonKey.Conformance.Tests`. Production projects do not reference a schema library in this phase.

Duplicate property detection cannot rely on ordinary schema validation because normal JSON DOM parsers can collapse duplicate names before a schema validator sees them.

Conformance fixtures are language assets. They are stored as JSON files plus a machine-readable manifest so future non-.NET runtimes can run the same cases and compare layer expectations.

## Consequences

The repository can test strict parsing, schema validation, and semantic validation together without merging their responsibilities.

Future runtimes can use the conformance suite without depending on .NET assemblies.

Schema changes require fixture and manifest updates, making contract drift visible during tests.
