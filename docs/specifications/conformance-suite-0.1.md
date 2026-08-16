# Conformance Suite 0.1

The Workflow Language 0.1 conformance suite lives under `tests/fixtures/conformance`.

The suite is intended for SkeletonKey itself and future non-.NET runtimes.

## Manifest

The manifest is `tests/fixtures/conformance/manifest.json`.

It contains:

- `formatVersion`: the conformance manifest format version.
- `cases`: the ordered list of fixture cases.

Each case contains:

- `id`: stable case identifier.
- `category`: fixture category.
- `file`: repository-relative fixture path below `tests/fixtures/conformance`.
- `serialization`: `success` or `failure`.
- `schema`: `valid`, `invalid`, or `not-applicable`.
- `semantic`: semantic expectation, or `null` when semantic validation is not applicable.

Semantic expectations contain `isValid`, `errors`, and `warnings`. Error and warning lists contain stable `SKWxxxx` codes.

## Fixture Categories

`valid` fixtures must deserialize, pass JSON Schema validation, and pass semantic validation with exactly the manifest diagnostics.

`serialization-invalid` fixtures must fail strict deserialization. Semantic validation is not run. Schema validation may be invalid, valid after normal DOM parsing, or not applicable for malformed JSON.

`schema-invalid` fixtures must be valid JSON and fail the normative JSON Schema. Strict deserialization may succeed or fail according to the manifest.

`semantic-invalid` fixtures must deserialize successfully, pass JSON Schema validation, and fail semantic validation with exactly the expected error and warning codes.

`warning` fixtures must deserialize successfully, pass JSON Schema validation, remain semantically valid, and produce exactly the expected warning codes with no errors.

## Layer Expectations

The suite preserves three distinct layers:

1. Strict JSON serialization: parser behavior, duplicate properties, unknown properties, required properties, JSON types, enum conversion, null handling, and canonical serialization.
2. JSON Schema validation: public machine-readable structure, required and allowed properties, simple formats, enum values, numeric boundaries, and extensibility boundaries.
3. Semantic validation: graph references, uniqueness, start-node rules, reachability, input default semantics, output source references, output mode compatibility, duration semantics, retry relationships, and designer references.

The suite includes valid fixtures for single, collection, stream, and mixed output modes, schema-invalid fixtures for output shape errors, and a semantic-invalid fixture for an unknown output source node.

Phase 0-7B adds fixtures for workflow invocation, structured bindings, literal wrappers, stream policies, invocation schema errors, and local invocation semantic diagnostics.

Phase 0-7C adds fixtures for expression wrappers, expression syntax and references, iteration bindings, graph-native control nodes, foreach policies, repeat and while limits, early return, and reserved control ports.

## Running In Another Runtime

Another runtime can execute the suite by:

1. Reading `manifest.json`.
2. Loading each fixture path relative to `tests/fixtures/conformance`.
3. Applying its strict parser and duplicate-property detector.
4. Applying the normative schema at `schemas/workflow/0.1/schema.json` when the fixture is parseable JSON.
5. Applying its semantic validator when deserialization succeeds and the manifest includes a semantic expectation.
6. Comparing produced semantic codes with the manifest.

Schema diagnostics do not need to match .NET library error messages.

## Determinism

The suite has no network dependency, no current-time dependency, no random fixture generation, no absolute paths, and no test ordering dependency.

Fixture files are source assets, not generated test data.

## Versioning

Conformance assets use version `0.1.0`.

Workflow specification version remains `0.1.0` in this phase. No compatibility aliases, migrations, or schemas for other versions are introduced.
## Phase 0-7D Addendum

The workflow conformance suite includes resource declarations, resource references, invocation resource mappings, locator references, and interaction request fixtures. A separate locator fixture suite under `tests/fixtures/locators` covers locator schema and semantic validation with its own manifest.
