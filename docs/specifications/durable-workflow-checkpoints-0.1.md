# Durable Workflow Checkpoints 0.1

Phase 0-17 adds optional durable execution checkpoints and process-restart resume at deterministic safe boundaries. The host supplies `IWorkflowCheckpointStore`; workflow documents never choose checkpoint paths.

## Contract

A checkpoint contains:

- format version `0.1`;
- execution, workflow, workflow-specification, and plan identities;
- a SHA-256 fingerprint of inputs and variable overrides;
- a monotonically increasing revision and host-clock timestamp;
- every planned step in deterministic plan order;
- step status, entry/control activation state, activation ordinal, terminal result metadata, and ordered multi-value output ports;
- terminal node-result and lifecycle-snapshot history in deterministic execution order, including repeated loop activations;
- execution attempt, activation, invocation, event-sequence, streamed-record, and duration counters;
- accumulated outcome/error state and an immutable final result for terminal checkpoints.

The filesystem store hashes execution IDs before using them as filenames, confines files to one host-owned root, verifies a SHA-256 payload checksum, serializes concurrent writers through an exclusive lock, rejects stale revisions, and replaces an existing checkpoint atomically.

Checkpoint payloads may contain workflow inputs and node outputs indirectly through fingerprints and persisted output values. The filesystem format provides integrity, not encryption. Hosts must place the checkpoint root on appropriately access-controlled or encrypted storage and apply their own retention policy.

## Runtime ordering and recovery

The runtime persists a `Running` checkpoint before invoking a node handler. It persists a safe checkpoint after the node finishes and its outputs/control activations have been applied. Therefore:

- a completed step present in a safe checkpoint is never executed again during resume;
- a crash before a handler begins resumes from the previous safe checkpoint;
- a crash after a handler may have begun leaves that step as `Running` and resume fails with `SKR3006` instead of risking a duplicate side effect;
- terminal checkpoint resume returns the original immutable result without invoking handlers;
- workflow/plan/input mismatches fail before execution.

`SKR3006` requires a future node-specific recovery policy or a deliberate new execution identity. Phase 0-17 does not guess whether an interrupted external side effect committed.

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

## Explicit exclusions

Checkpoint format 0.1 does not persist live browser/resource handles, pending in-memory interaction continuations, handler-local memory, or arbitrary external transactions. Non-terminal resume of workflows declaring runtime resources fails with `SKR3008`. Phase 0-18 extends the payload to format 0.2 with safe retry-attempt and not-before metadata while retaining 0.1 read compatibility. Parallel/distributed execution, node-specific compensation, and database checkpoint providers remain future work.
