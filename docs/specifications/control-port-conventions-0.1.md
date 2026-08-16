# Control Port Conventions 0.1

## Scope

This document defines the reserved control-port names used by SkeletonKey Workflow node definitions in specification version 0.1. It applies to built-in node catalogs and to host-authored node catalogs that want predictable validation, analysis, planning, and runtime behavior.

## Non-goals

This document does not define data-port schemas, dynamic data-port generation, retry behavior, parallel scheduling, durable persistence, or transport protocols. It also does not rename existing public ports.

## Public Contracts

Control ports are identified by stable, case-sensitive string IDs. A node may define input ports, output ports, and dynamic ports. Port IDs are unique within their own declared direction. Static input and static output ports may share the same ID only where the node model uses separate input and output namespaces.

The reserved control-port IDs are:

| Port | Direction | Meaning |
| --- | --- | --- |
| `main` | input | Standard control input that starts an action, branch, loop, interaction, invocation, or terminal node. |
| `continue` | output | Normal successful control output for action-like nodes. |
| `true` | output | Conditional true branch output. |
| `false` | output | Conditional false branch output. |
| `completed` | output | Successful loop completion output after iteration stops normally. |
| `default` | output | Switch fallback output when no explicit switch case matches. |

## JSON Shape

Catalog definitions express ports through `inputs`, `outputs`, and `dynamicPorts`. A standard action node uses:

```json
{
  "inputs": {
    "main": { "direction": "input" }
  },
  "outputs": {
    "continue": { "direction": "output" }
  }
}
```

Conditional and loop nodes use:

```json
{
  "outputs": {
    "true": { "direction": "output" },
    "false": { "direction": "output" }
  }
}
```

```json
{
  "outputs": {
    "body": { "direction": "output" },
    "completed": { "direction": "output" }
  }
}
```

## Validation Rules

Catalog validation must reject duplicate static port IDs within the same direction. Dynamic port rules must not define IDs that collide with static ports in the same direction at materialization time. Workflow validation and planning must treat control connections separately from data dependencies when a port carries the `control` role.

Built-in catalogs follow these conventions:

* Entry nodes produce `main`.
* Terminal nodes consume `main`.
* Action and interaction nodes consume `main` and normally produce `continue`.
* Branch nodes consume `main` and produce explicit branch outputs such as `true`, `false`, and `default`.
* Loop nodes consume `main`, `continue`, and `break`, and produce `body` and `completed`.

## Lifecycle

Port names are public contract. A built-in node version must not rename or remove a reserved port without creating a new node version and documenting compatibility behavior.

## Ownership

Catalog authors own the port definitions they publish. Runtime handlers must activate only documented output ports for their exact node definition version.

## Cancellation

Cancellation does not activate a normal control output. Cancelled nodes complete with a cancelled status and leave downstream scheduling to runtime cancellation behavior.

## Errors

If a handler tries to activate a port not declared by the catalog, that is a handler/catalog mismatch and must be surfaced as a structured workflow execution error. If a workflow references an unknown port, semantic validation must report a workflow validation issue.

## Security Boundaries

Control ports carry scheduling intent only. They must not carry secrets, browser handles, filesystem paths, artifact bytes, cookies, prompt text, or storage-state contents.

## Compatibility Rules

The reserved names are stable for specification version 0.1. Existing names such as loop `body`, loop `break`, and loop `continue` remain valid public contracts even though they are not part of the minimum reserved set listed above.

## Examples

An action node succeeds by activating `continue`. An `if` node activates exactly one of `true` or `false`. A `switch` node activates a matching dynamic case output or `default`. A loop node activates `body` for each iteration and `completed` after normal completion.

## Deferred Work

Future phases may define versioned compatibility rules for port aliases and richer control-flow metadata. Retry, parallel scheduling, durable persistence, and recovery are intentionally outside Phase 0-16.
