# Workflow JSON Format 0.1

This document describes the official JSON representation for the SkeletonKey workflow document model in specification version `0.1.0`.

A workflow that deserializes successfully is not necessarily semantically valid. The normative JSON Schema is documented separately in `workflow-json-schema-0.1.md`, semantic validation is documented in `workflow-validation-0.1.md`, and conformance fixtures are documented in `conformance-suite-0.1.md`.

## Encoding

Workflow JSON files are UTF-8. Files written by `WorkflowJsonSerializer` do not include a UTF-8 BOM, use LF line endings, and end with exactly one final newline.

## Root Properties

Root properties use this canonical order:

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

Optional root properties are `description`, `inputs`, `variables`, `outputs`, and `designer`. Omitted `inputs`, `variables`, and `outputs` default to empty dictionaries. Explicit `null` for those collections is rejected.

## Defaults

Omitted node `disabled` defaults to `false`. Omitted node `parameters` defaults to `{}`. Omitted execution-policy `onError` defaults to `fail`. Omitted retry `maxAttempts` defaults to `1`, and omitted retry `backoff` defaults to `1.0`. Omitted designer `positions` and `sizes` default to empty dictionaries.

## Enums

`WorkflowInputType` is represented as `string`, `integer`, `number`, `boolean`, `object`, and `array`.

`WorkflowOnError` is represented as `fail`, `continue`, and `stop`.

Unknown enum text and numeric enum values are rejected.

## Null Behavior

Explicit `null` is allowed only for nullable properties and JSON values that permit it, such as workflow variable values and input defaults. Required strings, required objects, `nodes`, `connections`, null node entries, and null connection entries are rejected.

## Unknown And Duplicate Properties

Unknown properties are rejected for all core workflow structures. Arbitrary properties are allowed only inside node `parameters`, workflow variable values, and input default values.

Duplicate property names are rejected recursively, including inside arbitrary parameter and variable JSON.

## Inputs

Each input definition requires `type`. Optional properties are `required`, `default`, and `description`. `required` defaults to `false`.

## Variables

`variables` is an object whose values may be any valid JSON value, including `null`.

## Outputs

`outputs` is an object keyed by output name. Output names use the same format as input and variable names.

Each output definition uses a required `mode`:

- `single`: requires `from`, where `from` is a workflow endpoint.
- `collection`: requires `from`, where `from` is a workflow endpoint.
- `stream`: requires `channel`, where `channel` matches `^[a-z][a-z0-9.-]*$`.

Optional `description` is allowed for every output mode. Single and collection outputs do not allow `channel`; stream outputs do not allow `from`.

## Nodes

Node properties use this canonical order:

1. `id`
2. `type`
3. `typeVersion`
4. `displayName`
5. `description`
6. `disabled`
7. `parameters`
8. `policy`

Required node properties are `id`, `type`, and `typeVersion`.

## Workflow Invocation Parameters

The reserved node type `workflow.invoke` uses parameter property order `workflow`, `inputs`, then `streams`.

`workflow` is required and contains `id` plus optional exact `version`. `inputs` is an object of workflow values. `streams` declares `forward`, `suppress`, or `map`.

Ordinary node parameters remain literal JSON except for reserved language node types. The structured workflow-value rules apply where the language explicitly defines workflow values, such as `workflow.invoke` inputs, control conditions, loop inputs, return messages, and return data.

## Workflow Values and Bindings

Workflow values may contain literal JSON, nested arrays and objects, `$binding` wrappers, `$expression` wrappers, and `$literal` wrappers.

`$binding` property order is `source`, `name`, `node`, `port`, `iteration`, `path`, `onMissing`, `default`.

`$expression` preserves expression text exactly as a JSON string value. JSON string escaping is canonicalized by `System.Text.Json`, but the deserialized expression text is not parsed or reformatted by serialization.

`$literal` escapes reserved wrapper names for literal application data.

## Control Flow Parameters

Reserved control-node parameter order is canonicalized for `flow.if`, `flow.switch`, `flow.foreach`, `flow.repeat`, `flow.while`, and `core.return`.

Switch case order is preserved. Loop and return contracts are serialized as declarations only and do not imply runtime execution.

## Connections And Endpoints

Connection properties use the order `from`, then `to`. Endpoint properties use the order `node`, then `port`. Both endpoint properties are required.

## Policies And Retry Declarations

Execution policy properties use the order `timeout`, `onError`, `retry`. Retry properties use the order `maxAttempts`, `delay`, `backoff`, `maxDelay`.

Policy declarations do not execute behavior in this phase.

## Designer Metadata

Designer metadata uses the order `positions`, then `sizes`. Positions contain `x` and `y`. Sizes contain `width` and `height`. Designer metadata has no runtime semantics.

## Error Paths

Serialization exceptions expose JSON Pointer paths where possible, such as `/nodes/1/typeVersion` or `/connections/0/to/node`. Some syntax failures may only have the best path reported by `System.Text.Json`.

## Canonical Example

```json
{
  "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
  "specVersion": "0.1.0",
  "id": "minimal-workflow",
  "name": "Minimal Workflow",
  "inputs": {},
  "variables": {},
  "nodes": [
    {
      "id": "start",
      "type": "core.start",
      "typeVersion": 1,
      "disabled": false,
      "parameters": {}
    },
    {
      "id": "log",
      "type": "core.log",
      "typeVersion": 1,
      "disabled": false,
      "parameters": {
        "message": "Hello from SkeletonKey",
        "level": "information"
      }
    },
    {
      "id": "end",
      "type": "core.end",
      "typeVersion": 1,
      "disabled": false,
      "parameters": {}
    }
  ],
  "connections": [
    {
      "from": {
        "node": "start",
        "port": "main"
      },
      "to": {
        "node": "log",
        "port": "main"
      }
    },
    {
      "from": {
        "node": "log",
        "port": "main"
      },
      "to": {
        "node": "end",
        "port": "main"
      }
    }
  ],
  "outputs": {
    "result": {
      "mode": "single",
      "from": {
        "node": "log",
        "port": "main"
      },
      "description": "The final example result."
    }
  }
}
```

## Semantic Validation

Deserialization does not validate start nodes, graph reachability, cycle rules, connection references, output source references, stream channel semantics beyond JSON type, port compatibility, node availability, or node parameter schemas.

`SkeletonKey.Validation` validates the parsed `WorkflowDocument` for workflow language 0.1 semantics. Semantic validity still does not imply that node types or ports are available at runtime.

## JSON Schema Validation

The normative Workflow Language 0.1 JSON Schema describes public document structure for tools and cross-runtime conformance. It does not replace strict parsing or semantic validation.
## Phase 0-7D Addendum

Reserved workflow-value wrappers are `$binding`, `$expression`, `$resource`, `$locator`, and `$literal`. `$literal` prevents interpretation of reserved wrapper names as structured workflow values. `workflow.invoke` parameters are serialized in canonical order `workflow`, `inputs`, `resources`, then `streams`.
