# Durable State 0.1

## Status

Preview implementation contract for the Phase 1 cross-execution durable state capability defined by ADR 0030.

## Purpose

Durable State stores bounded JSON-compatible facts across independent workflow executions. It is distinct from execution checkpoints: checkpoints recover one interrupted execution; durable state is an application-neutral key/value capability intentionally observable by later executions.

## Package Boundary

```text
SkeletonKey.State.Abstractions
SkeletonKey.State.BuiltIns
SkeletonKey.State.Sqlite
```

The abstraction layer exposes no SQLite, PostgreSQL, Redis, or provider-specific connection type. The first concrete provider is local SQLite.

## Node Identities

All initial nodes use `typeVersion: 1`:

```text
state.get
state.put
state.exists
state.delete
state.compareExchange
```

Every node has a `main` control input and activates `continue` on success.

## Address Model

Every state entry is identified by:

```text
scope + namespace + key
```

All namespace/key comparisons are ordinal and case-sensitive at the SkeletonKey contract level.

### Workflow scope

```json
{
  "scope": "workflow",
  "key": "item/123"
}
```

The namespace is derived from the current `WorkflowId`. Independent executions of the same workflow therefore observe the same state key.

### Host scope

```json
{
  "scope": "host",
  "key": "shared/item/123"
}
```

The namespace is supplied by the consuming host when it creates the state handlers. Host identity is not stored in the workflow artifact.

### Custom scope

```json
{
  "scope": "custom",
  "namespace": "tenant-a",
  "key": "item/123"
}
```

Custom namespaces are explicit workflow data and must be non-empty.

The default scope is `workflow`.

## Stored Entry

A durable entry contains:

- JSON-compatible `value`, including explicit JSON null;
- opaque `version` token;
- `updatedAtUtc` timestamp recorded by the provider.

Consumers must treat the version as opaque and use exact equality only.

## Operations

### `state.get`

Returns:

- `found`
- `value`
- `version`
- `updatedAtUtc`

Missing entries return `found = false` and null entry fields.

### `state.put`

Unconditionally creates or replaces the key and returns a new `version` and `updatedAtUtc`.

### `state.exists`

Returns `exists` and the current `version` when present.

### `state.delete`

Deletes the key and returns `deleted` indicating whether a row existed.

### `state.compareExchange`

Parameters:

- `key`, scope parameters, and `value` as above;
- `expectedVersion`, optional.

Semantics:

- omitted/null `expectedVersion` means create only if the key is absent;
- non-null `expectedVersion` means replace only if the existing entry has exactly that version;
- success returns `succeeded = true` plus the resulting entry;
- failure returns `succeeded = false` plus the currently observed entry, if any.

This operation is the Phase 1 primitive for race-safe deduplication/claiming. A read followed by a separate write is not equivalent.

## SQLite Provider

The SQLite provider owns a local database path supplied by host configuration. The path is not workflow business semantics.

Schema version 1 contains one table keyed by `(scope, namespace, key)`. The provider:

- creates the database and schema deterministically;
- enables WAL mode;
- uses bounded SQLite busy timeout behavior;
- represents values as JSON text;
- generates a fresh opaque version token for every successful mutation;
- implements create-if-absent with atomic `INSERT ... ON CONFLICT DO NOTHING`;
- implements versioned replace with atomic `UPDATE ... WHERE version = expectedVersion`;
- never reuses a deleted entry's stale version token when the key is recreated.

## Failure Codes

- `SKS1001` — invalid state request.
- `SKS1002` — provider/store failure.
- `SKS1003` — store remained busy/locked beyond the configured bound.
- `SKS1004` — caller cancellation.
- `SKS1005` — unsupported store schema.

## Checkpoint Separation

The durable state provider is not an execution-checkpoint participant. Enabling checkpoint/resume must not implicitly mutate durable state, and using durable state must not require checkpoint/resume.

## Composition

A consuming host chooses the concrete store and host namespace:

```csharp
using IWorkflowStateStore store = new SqliteWorkflowStateStore("./data/state.db");
IReadOnlyList<INodeHandler> handlers = StateBuiltInRuntimeHandlers.Create(store, hostNamespace: "worker-a");
IReadOnlyList<WorkflowNodeDefinition> definitions = StateBuiltInWorkflowNodeCatalog.Catalog.Definitions;
```

The consuming host owns the state-store lifetime and storage path.
