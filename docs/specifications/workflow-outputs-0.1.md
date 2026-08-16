# Workflow Outputs 0.1

Workflow outputs declare what a workflow exposes to its host.

The workflow language version remains `0.1.0`.

## JSON Shape

`outputs` is an optional root object. Omitted outputs default to `{}`. Explicit `null` is invalid.

Output names must match:

```text
^[A-Za-z_][A-Za-z0-9_-]*$
```

Each output definition requires `mode`.

## Modes

`single` declares one final value:

```json
{
  "mode": "single",
  "from": { "node": "log", "port": "main" }
}
```

`collection` declares one final collection:

```json
{
  "mode": "collection",
  "from": { "node": "log", "port": "items" }
}
```

`stream` declares records emitted on a channel:

```json
{
  "mode": "stream",
  "channel": "records.done"
}
```

Stream channel names must match:

```text
^[a-z][a-z0-9.-]*$
```

`description` is optional for every mode.

## Validation

Single and collection outputs require `from` and must not declare `channel`.

Stream outputs require `channel` and must not declare `from`.

Semantic validation checks output names, output mode compatibility, source node references, source port format, and stream channel format.

The validator does not check output port existence or output value type compatibility because node definitions and port catalogs are not implemented in this phase.

Invocation stream mapping targets must refer to stream channels declared by the parent workflow outputs. Source child stream channels are not validated until referenced workflows can be resolved.

## Runtime Boundary

Outputs are declarations only. Phase 0-7A does not bind values, execute nodes, dispatch events, or invoke subworkflows.
