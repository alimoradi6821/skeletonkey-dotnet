# SkeletonKey 0.1.0 GA Release Runbook

## 1. Build and verify

Use a clean source tree on supported Windows x64 and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-030.ps1
```

Do not ship if any prior phase, security audit, soak, canary, rollback, recovery, or storage-failure gate fails.

## 2. Review machine-readable evidence

Review these files before promotion:

- `artifacts/release/skeletonkey-0.1.0-win-x64.ga.json`
- `artifacts/release/skeletonkey-0.1.0-win-x64.sbom.cdx.json`
- `artifacts/release/skeletonkey-0.1.0-win-x64.provenance.json`
- `artifacts/release/skeletonkey-0.1.0-win-x64.signing-readiness.json`
- `artifacts/soak/phase-028-soak-report.json`
- `artifacts/canary/phase-029-canary-report.json`

## 3. Signing for public distribution

Keep the PFX and password outside the repository. Sign the self-contained published package with `build/sign-release.ps1`, regenerate the release ZIP and Phase 27 metadata, then rerun Phase 30 with `-RequireSignedRelease`.

## 4. Agent rollout

1. Stage the candidate into the inactive blue/green slot.
2. Verify the Agent bundle manifest and archive SHA-256.
3. Run canary tasks on the candidate slot.
4. Promote the candidate by the deployment-state pointer only after canary success.
5. Keep durable checkpoints, artifacts, and logs under the shared `state` directory outside versioned slots.
6. Roll back the pointer to the previous slot if post-promotion monitoring detects a regression.

Never overwrite the currently active slot in place.

## 5. Failure handling

- `SKR3003`: checkpoint integrity failure; do not resume from the corrupted checkpoint.
- `SKR3005`: checkpoint storage unavailable; restore host storage/permissions before retrying.
- `SKR3006`: process stopped while a node was running; require explicit recovery rather than automatic replay.
- `SKR2029`: artifact persistence failure; restore host storage before repeating an artifact-producing action.
- `SKP2205`: plugin hash mismatch; reject the plugin package.

## 6. Rollback

Rollback changes only the active Agent slot pointer. Do not delete the failed candidate until diagnostic evidence is collected. Durable execution state remains outside slots and must not be removed as part of rollback.
