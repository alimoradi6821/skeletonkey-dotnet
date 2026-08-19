# 0027: Resumable Runtime Resources

## Status

Accepted for Phase 24.

## Decision

Durable checkpoint format 0.3 can contain provider-owned reconstruction state for runtime resources that have already been activated. The runtime owns the checkpoint envelope and resource identity; each provider owns a separately versioned JSON payload through `IWorkflowRuntimeResourceCheckpointParticipant` and reconstructs an instance through `IWorkflowRuntimeResourceRecoveryProvider`.

At every safe checkpoint boundary, the runtime asks each live instance for reconstructable state. A null state is persisted as an explicit non-resumable marker. Resume validates exact resource name and kind, requires a recovery-capable provider, reconstructs resources before scheduling any remaining step, and fails closed with `SKR3008` or `SKR3009` when that contract cannot be satisfied. Formats 0.1 and 0.2 remain readable, but a non-terminal legacy checkpoint that used resources cannot be resumed.

The first recovery-capable provider is the ephemeral Playwright `web.page` resource. It captures bounded browser storage state, stable page IDs, active-page identity, open page URLs, closed/stale reference metadata, and ID counters. Recovery creates a new browser and context, imports storage state, and re-navigates open pages under the configured navigation policy. It never serializes Playwright objects or operating-system handles.

Persistent browser profiles and contexts with a pending dialog return a non-resumable marker. Desktop application handles, suspended human-interaction continuations, interrupted `Running` nodes, handler-local memory, downloads/uploads in progress, and arbitrary external transactions are not reconstructed.

## Consequences

- Workflow documents stay provider-neutral and never contain recovery payloads.
- Provider payloads are immutable snapshots with their own format version and strict bounds.
- Browser storage state can contain cookies and authentication data; checkpoint storage provides integrity, not encryption.
- Page reconstruction repeats navigation to captured URLs, so hosts must use navigation policy and design workflows around safe checkpoint boundaries.
- Existing at-most-once behavior remains unchanged: a checkpoint with a `Running` step still fails with `SKR3006`.
- Durable parallel-frontier and distributed scheduling remain out of scope.
