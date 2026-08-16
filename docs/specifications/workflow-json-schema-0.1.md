# Workflow JSON Schema 0.1

The normative Workflow Language 0.1 JSON Schema is located at `schemas/workflow/0.1/schema.json`.

Schema URI:

```text
https://schemas.skeletonkey.dev/workflow/0.1/schema.json
```

Draft:

```text
JSON Schema Draft 2020-12
```

The local repository file is authoritative during pre-alpha development. Public schema hosting is not implemented yet.

## Root Structure

The schema describes a workflow document object with properties in canonical JSON order:

1. `$schema`
2. `specVersion`
3. `id`
4. `name`
5. `description`
6. `inputs`
7. `variables`
8. `nodes`
9. `connections`
10. `outputs`
11. `designer`

Required root properties are `$schema`, `specVersion`, `id`, `name`, `nodes`, and `connections`.

The root uses `additionalProperties: false`.

## Definitions

Reusable structures live under `$defs`, including workflow IDs, node IDs, input, variable, and output names, node types, port names, output channel names, input definitions, output definitions, workflow references, workflow values, binding wrappers, expression wrappers, literal wrappers, invocation stream policies, control-node parameters, return outcomes, nodes, connections, endpoints, execution policies, retry policies, designer metadata, positions, and sizes.

All `$ref` values are local references. The schema does not require network access.

## Extensibility Boundaries

Core workflow structures are closed with `additionalProperties: false`.

Workflow variables and node parameters are intentionally extensible. Variable values may be any JSON value, and node parameters may contain arbitrary nested JSON inside an object.

The schema does not define node-specific parameter names or values.

## Defaults

Defaults such as `inputs = {}`, `variables = {}`, `disabled = false`, `parameters = {}`, `onError = fail`, `maxAttempts = 1`, and `backoff = 1.0` are annotations only.

Schema validation does not mutate workflow documents or apply defaults.

## Input Default Constraints

Input names must match the workflow language input-name pattern.

Required inputs must not declare a `default` property.

Non-null input defaults must match the declared input type. Explicit `null` defaults are allowed for optional inputs.

## Nodes And Connections

Node IDs, node types, type versions, endpoint node IDs, and endpoint ports receive basic structural validation.

The schema does not enforce unique node IDs, existing endpoint references, duplicate connection rules, or graph reachability.

## Outputs

The schema validates output object shape, output names, output modes, required `from` endpoints for single and collection outputs, required stream `channel` values, and incompatible `from` or `channel` properties.

The schema does not enforce whether output source nodes exist. That remains a semantic validation responsibility.

## Invocation and Bindings

The schema reserves `workflow.invoke` parameter shape, including `workflow`, `inputs`, and `streams`.

The schema validates binding wrapper shape, literal wrapper shape, allowed binding sources, binding source-specific properties, and invocation stream mode shape.

Exact Semantic Version 2.0 validation, local binding references, JSON Pointer read semantics, missing-value policy relationships, and mapped parent stream target existence are semantic validation responsibilities.

## Expressions, Control Flow, And Iteration

The schema reserves `$expression` wrapper shape and the parameter shapes for `flow.if`, `flow.switch`, `flow.foreach`, `flow.repeat`, `flow.while`, and `core.return`.

Expression syntax, expression references, condition suitability, iteration reference existence, foreach policy relationships, repeat count semantics, while limit semantics, reserved port compatibility, and return terminal behavior are semantic validation responsibilities.

## Execution Policies

The schema validates execution policy shape, `onError` enum values, retry `maxAttempts >= 1`, and retry `backoff >= 1`.

Duration syntax and relationships such as `maxDelay >= delay` remain semantic validation responsibilities.

## Designer Metadata

Designer positions and sizes are structurally validated.

Size width and height must be numbers greater than zero. Designer references to existing nodes remain semantic warnings.

## Excluded Rules

The schema intentionally excludes duplicate JSON property detection, unique node IDs, exact start-node count, disabled start-node checks, existing source and target nodes, existing output source nodes, local binding references, expression parsing, expression references, iteration references, referenced workflow existence, referenced workflow version availability, child input compatibility, duplicate connections, incoming connections to start nodes, outgoing connections from end nodes, outgoing connections from return nodes, reachability, cycles, port existence, reserved control port compatibility, node type availability, ordinary node parameter schemas, retry delay relationships, designer references, and runtime handler availability.

## Relationship To Strict Deserialization

Strict deserialization is implemented by `SkeletonKey.Serialization.Json`. It owns parser behavior, duplicate-property rejection, unknown-property rejection, null handling, enum conversion, and canonical serialization.

Some schema-invalid documents may still deserialize successfully because the schema intentionally provides editor-oriented structural feedback beyond parsing.

For example, strict deserialization may treat explicit `null` for some optional object declarations as absent, while the normative schema requires public Workflow 0.1 documents to omit those properties or provide non-null objects.

## Relationship To Semantic Validation

Semantic validation is implemented by `SkeletonKey.Validation`.

Some schema-valid documents may be semantically invalid. Passing JSON Schema validation does not imply that the workflow can execute or that all node types and ports are available at runtime.
## Phase 0-7D Addendum

Workflow Schema 0.1 now includes root `resources`, resource definitions, `$resource`, `$locator`, invocation resource mappings, and the reserved `interaction.request` parameter shape. The workflow specification version remains `0.1.0`; no workflow schema `0.2` is introduced.
