# Phase 0-17 Durable Execution Checkpoints and Resume

Status: implemented; repository verification required before release tagging.

Phase 0-17 introduces a provider-neutral `IWorkflowCheckpointStore` contract in `SkeletonKey.Runtime` and an atomic, integrity-protected implementation in `SkeletonKey.Artifacts.FileSystem`. Runtime persistence is opt-in and host-owned. No checkpoint path or storage capability is exposed to workflow nodes.

The default runtime checkpoints scheduler state before and after each top-level node activation. A pre-handler checkpoint records `Running`; a post-handler checkpoint records terminal node status, propagated controls, and complete ordered output-port values. Resume restores completed output maps, activation ordinals, counters, outcomes, and event sequence before deterministic scheduling continues. Terminal checkpoints are replay-free and return the persisted result directly.

The recovery policy is intentionally at-most-once safe. A checkpoint containing a `Running` step is rejected with `SKR3006`, because the runtime cannot determine whether an external side effect committed before the process stopped. This prevents silent duplicate automation. Live resource handles and in-memory interaction continuations are also not serializable in format 0.1; non-terminal resource resume is rejected with `SKR3008`.

The filesystem provider uses a SHA-256 hash of the execution ID as the filename, a checksummed Base64 payload envelope, an exclusive lock file, optimistic revision comparison, a write-through temporary file, and atomic filesystem replacement. Corrupt payloads and stale writers fail with stable checkpoint codes.

The Runner adds `--checkpoint-directory` to `run` and a `resume` command. Runner plan identity is derived from canonical workflow JSON so edited workflow content cannot resume an older checkpoint accidentally.

Phase 0-17 does not add retry, compensation, parallel or distributed scheduling, database stores, durable human interaction, plugin discovery, desktop automation, or recovery of Playwright objects.
