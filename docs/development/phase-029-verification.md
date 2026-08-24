# Phase 29 Verification

Phase 29 is the deployment and Agent-integration gate reused by the final `0.1.0` GA regression chain.

## Acceptance

Run on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-029.ps1
```

The gate must pass Phase 0-28 regression first, then prove all of the following using the published self-contained Runner rather than source-hosted execution:

1. A hash-closed Agent runtime bundle is produced and verified.
2. The bundle can be staged into an inactive blue/green slot without overwriting the active slot.
3. Promotion is pointer-based and preserves the prior slot for rollback.
4. Agent task routing executes the workflow from the active slot with host-owned durable checkpoints outside the versioned slot. Active bundle/runtime hashes and optional task `requiredVersion` affinity are checked before execution.
5. A candidate slot can be promoted and the previous slot can be restored by rollback.
6. A process-tree kill at a persisted safe retry boundary can resume with the same execution identity. The ephemeral Playwright page must be reconstructed from checkpoint state and the workflow must finish successfully.
7. A process-tree kill while a handler is already running must not replay the ambiguous operation. Resume must fail closed with `SKR3006`.
8. A machine-readable canary report is emitted at `artifacts/canary/phase-029-canary-report.json`.

## Agent bundle layout

```text
SkeletonKey Agent Runtime bundle
├── agent-bundle.json
├── agent-runtime.json
├── runtime/
│   ├── skeletonkey.exe
│   ├── manifest.json
│   └── SHA256SUMS
├── workflows/
├── locators/
└── plugins/
```

Host-owned state is deliberately outside versioned slots:

```text
install-root/
├── deployment-state.json
├── slots/
│   ├── blue/
│   └── green/
└── state/
    ├── checkpoints/
    ├── artifacts/
    └── logs/
```

This separation permits version rollback without losing durable execution state.

## Recovery contract

A safe checkpoint is resumable. A checkpoint showing an in-flight `Running` handler is intentionally not replayed because SkeletonKey cannot know whether an external side effect committed before the crash. That case returns `SKR3006` and requires explicit higher-level recovery or a new execution identity.

Phase 29 therefore validates both recovery and fail-closed behavior; it does not claim exactly-once semantics for arbitrary external systems.
