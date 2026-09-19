# Durable Workflow Checkpoints 0.1

Phase 0-17 adds optional durable execution checkpoints and process-restart resume at deterministic safe boundaries. Phase 18 extends retry metadata, Phase 24 adds provider-owned runtime-resource reconstruction, and Phase 5 production hardening adds explicit external side-effect dispatch certainty and reconciliation. The host supplies `IWorkflowCheckpointStore`; workflow documents never choose checkpoint paths.

## Contract

A checkpoint contains:

- format version `0.4` (`0.1`, `0.2`, and `0.3` remain readable);
- execution, workflow, workflow-specification, and plan identities;
- a SHA-256 fingerprint of inputs and variable overrides;
- a monotonically increasing revision and host-clock timestamp;
- every planned step in deterministic plan order;
- step status, entry/control activation state, activation ordinal, terminal result metadata, and ordered multi-value output ports;
- terminal node-result and lifecycle-snapshot history in deterministic execution order, including repeated loop activations;
- execution attempt, activation, invocation, event-sequence, streamed-record, and duration counters;
- accumulated outcome/error state and an immutable final result for terminal checkpoints.
- ordered live-resource entries containing resource name, kind, resumability, and an immutable provider-versioned JSON state payload;
- per-step side-effect dispatch state: `None`, `NotDispatched`, `DispatchUncertain`, or `Completed`.

The filesystem store hashes execution IDs before using them as filenames, confines files to one host-owned root, verifies a SHA-256 payload checksum, serializes concurrent writers through an exclusive lock, rejects stale revisions, and replaces an existing checkpoint atomically.

Checkpoint payloads may contain workflow inputs and node outputs indirectly through fingerprints and persisted output values. Resource payloads can additionally contain browser cookies and origin storage. The filesystem format provides integrity, not encryption. Hosts must place the checkpoint root on appropriately access-controlled or encrypted storage and apply their own retention policy.

## Runtime ordering and recovery

The runtime persists a `Running` checkpoint before invoking a node handler. It persists a safe checkpoint after the node finishes and its outputs/control activations have been applied. Therefore:

- a completed step present in a safe checkpoint is never executed again during resume;
- a crash before a handler begins resumes from the previous safe checkpoint;
- ordinary interrupted handlers still fail with `SKR3006`;
- external side-effect handlers persist `NotDispatched` before dispatch and `DispatchUncertain` immediately before handler invocation;
- `NotDispatched` may replay safely;
- `DispatchUncertain` requires an `INodeSideEffectRecoveryHandler` or fails with `SKR3010`; it is never blindly replayed;
- terminal checkpoint resume returns the original immutable result without invoking handlers;
- workflow/plan/input mismatches fail before execution.
- live resources are reconstructed before the next remaining step is scheduled;
- a previously activated resource without a resumable snapshot or recovery provider fails closed instead of being silently recreated empty.

`SKR3006` remains the fail-closed result for interrupted nodes without an explicit side-effect recovery contract. `SKR3010` represents a specifically identified external operation whose dispatch may have occurred but whose external result is not durably known.

## Runner

Create checkpoints during execution:

```powershell
skeletonkey run --file workflow.json --execution-id order-42 --checkpoint-directory .\checkpoints
```

Resume the same execution:

```powershell
skeletonkey resume --file workflow.json --execution-id order-42 --checkpoint-directory .\checkpoints
```

The same workflow content, inputs, variable overrides, execution ID, and checkpoint directory must be supplied.

## Stable errors

| Code | Meaning |
|---|---|
| `SKR3001` | Unsupported checkpoint format version |
| `SKR3002` | Execution, workflow, plan, or request fingerprint mismatch |
| `SKR3003` | Invalid or missing checkpoint payload |
| `SKR3004` | Optimistic revision conflict |
| `SKR3005` | Checkpoint store read/write failure |
| `SKR3006` | Process stopped while a node was running; explicit recovery required |
| `SKR3007` | Persisted step set differs from the current plan |
| `SKR3008` | Non-terminal resume requires unsupported live resource recovery |
| `SKR3009` | Runtime resource checkpoint capture or reconstruction failed |
| `SKR3010` | External side-effect outcome is uncertain and explicit reconciliation is required |

## Explicit exclusions

Checkpoint format 0.2 adds safe retry-attempt and not-before metadata. Format 0.3 adds resource checkpoint entries. Format 0.4 adds side-effect dispatch certainty while retaining 0.1/0.2/0.3 read compatibility. Format 0.3 resource checkpoints remain reconstructable. Legacy non-terminal checkpoints that already used runtime resources fail with `SKR3008` because they contain no reconstruction state.

Phase 24 reconstructs ephemeral Playwright pages only when no dialog is pending. It does not persist raw browser objects, persistent-profile ownership, desktop application handles, pending in-memory interaction continuations, handler-local memory, interrupted external operations, or arbitrary external transactions. Page recovery imports bounded storage state and re-navigates captured open-page URLs under the configured navigation policy. Parallel/distributed execution, node-specific compensation, and database checkpoint providers remain future work.
