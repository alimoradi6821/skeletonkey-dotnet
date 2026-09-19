# Production Hardening: Diagnostics, Leases, and Side-Effect Recovery 0.1

## Status

Implemented Phase 5 capability contract.

## Failure Diagnostics

Hosts may provide `INodeFailureDiagnosticContributor` implementations. A contributor:

- declares a stable provider-neutral identifier;
- decides whether it applies to one exact node definition;
- captures evidence only after the node has already failed;
- cannot change the success/failure decision made by the handler.

The runtime bounds diagnostics by contributor count, JSON depth/collection size, individual string length, and aggregate serialized character count. Keys commonly associated with credentials such as authorization, password, secret, token, cookie, API key, storage state, and prompt text are replaced with `[REDACTED]`.

Contributor exceptions are converted to bounded `captureFailed` evidence. They never replace the original workflow error.

The default runner composes the Playwright contributor. It records only bounded active-page metadata. HTTP/HTTPS URLs have user-info, query strings, and fragments removed before diagnostic attachment. Non-HTTP URLs are reduced to their scheme.

## Durable Fenced Leases

`IWorkflowLeaseStore` provides:

```text
TryAcquireLeaseAsync
RenewLeaseAsync
ReleaseLeaseAsync
```

An active `WorkflowLease` contains:

```text
address
leaseId
ownerId
fencingToken
acquiredAtUtc
expiresAtUtc
```

Fencing tokens are monotonically increased for the same durable address whenever ownership is newly acquired or reclaimed. A stale owner cannot renew or release a replacement lease because both lease identity and fencing token must match an unexpired row.

SQLite implements acquisition with one database-side `INSERT ... ON CONFLICT DO UPDATE ... WHERE` mutation. Correctness therefore does not rely on a process-local mutex.

Workflow nodes are:

```text
state.leaseAcquire
state.leaseRenew
state.leaseRelease
```

They reuse the same workflow/host/custom addressing model as ordinary durable state.

## Side-Effect Dispatch Checkpoints

Handlers that may produce externally observable effects implement `IExternalSideEffectNodeHandler`.

For those handlers checkpoint format 0.4 records one of:

```text
None
NotDispatched
DispatchUncertain
Completed
```

Before external dispatch the runtime persists `NotDispatched`, then persists `DispatchUncertain`, and only then invokes the handler. This creates two important restart boundaries:

- `NotDispatched` proves execution may safely replay the operation.
- `DispatchUncertain` means the external operation may already have happened and must not be blindly replayed.

If the handler returns normally, the in-memory state becomes `Completed`. The next safe checkpoint records that result. A process stop after external completion but before that checkpoint intentionally leaves `DispatchUncertain`, because the durable runtime record cannot prove the external outcome.

## Explicit Reconciliation

A side-effect handler may additionally implement `INodeSideEffectRecoveryHandler`.

Its reconciliation result is one of:

- `NotAttempted` — external dispatch definitely did not occur; the normal handler may run once;
- `Completed` — the external effect completed; recovered outputs are validated and committed without rerunning the handler;
- `Uncertain` — the outcome still cannot be proven.

Without a reconciler, or when reconciliation remains uncertain, resume fails with `SKR3010`. The runtime does not call the normal handler.

An unexpected exception or timeout after external dispatch also becomes `SKR3010` and is a terminal recovery barrier. Ordinary retry/on-error behavior is not used to silently bypass that uncertainty. An explicit `NodeHandlerResult.Failed` returned by a handler is treated as a definitive handler result and may still participate in the workflow's configured retry policy.

## Built-In Classification

The initial external-side-effect classification includes:

- `http.request`;
- durable state mutators and lease mutations;
- web navigation/actions/form mutation, page lifecycle, upload/download-producing clicks, dialog responses, cookie mutation, storage-state import, scrolling/focus/input actions.

Read/query operations such as collection extraction, text/attribute/count reads, cookie reads, screenshots, waits, and page listing remain ordinary replayable handlers.

## Compatibility

Checkpoint format `0.4` adds side-effect dispatch certainty. Formats `0.1`, `0.2`, and `0.3` remain readable.

Format `0.3` continues to support runtime-resource reconstruction. Older formats without resource checkpoint state still fail closed when a non-terminal execution requires resource recovery.

SQLite durable-state schema version `2` adds the lease table. Existing schema-version-1 databases are upgraded in place while preserving key/value entries.

## Verification

Phase 5 verification includes:

- diagnostic secret-key redaction;
- contributor-failure isolation;
- diagnostics disable policy;
- concurrent SQLite lease acquisition with exactly one winner;
- lease expiry/reclaim with a strictly higher fencing token;
- stale-owner renew/release rejection;
- crash injection at `NotDispatched`;
- crash injection at `DispatchUncertain` with no reconciler;
- reconciliation that confirms completion without invoking the side effect again;
- reconciliation that proves non-dispatch and permits exactly one normal replay.
