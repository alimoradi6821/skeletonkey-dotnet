# ADR 0005: Dedicated JSON Serialization Layer

## Status

Accepted for Phase 0-4.

## Context

SkeletonKey now has an immutable workflow document model. Phase 0-4 adds JSON parsing and canonical JSON writing without making the domain model depend on serializer mechanics.

## Decision

JSON serialization lives in `SkeletonKey.Serialization.Json`, a dedicated project that depends on `SkeletonKey.Workflow`. The workflow model project does not reference the serializer.

Domain models remain free of serializer attributes. This keeps the in-memory representation independent from a specific wire format and avoids weakening constructor-based immutability.

The serializer uses `System.Text.Json` from the .NET platform. No external JSON, JSON Schema, dependency injection, logging, or options packages are introduced.

Parsing is strict. Unknown properties and duplicate properties are rejected because ambiguous documents are difficult to review, diff, sign, hash, or validate later. Duplicate detection is applied recursively, including arbitrary parameter and variable JSON.

Serialized JSON has canonical property order for stable diffs, deterministic examples, and future snapshot or signing work. Node and connection list order is preserved. Dictionary insertion order is preserved when possible, but dictionary order has no runtime meaning.

Deserialization is syntax and shape parsing only. Semantic workflow validation, such as start-node rules, connection references, graph reachability, and node parameter schema validation, remains separate.

JSON-backed values remain deeply cloned by the domain model. The serializer reads model JSON properties as copies and does not expose mutable serializer state.

File writes use a temporary file in the target directory and move it into place after a successful write. This avoids partial target files when serialization or writing fails.

## Consequences

The serializer can be used concurrently through a single instance. The strict parser rejects some JSON accepted by permissive parsers, but that is intentional for workflow documents.
