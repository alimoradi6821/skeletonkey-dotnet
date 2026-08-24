# Phase 28 Verification — Stress, Soak, and Resource-Leak Validation

Phase 28 adds long-running reliability evidence around the Phase 27 release candidate. It does not add automation features and does not weaken any earlier release, security, supply-chain, browser, desktop, checkpoint, or plugin gate.

## Acceptance command

Run from an interactive Windows checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-028.ps1
```

Expected terminal message:

```text
Phase 0-28 stress, soak, and resource-leak verification passed.
```

The local gate reruns Phase 27 first. That transitively includes the Phase 0-26 regression path, the interactive FlaUI/Notepad smoke, published Runner fault injection, SBOM generation, provenance, and code-signing readiness.

## In-process Runner soak

`Phase28SoakTests` repeatedly executes a minimal real workflow through `SkeletonKeyRunner` in one long-lived test process. Warm-up runs happen before the baseline is captured. After 250 measured executions and forced full collection:

- retained managed-memory growth must remain at or below 64 MiB;
- operating-system handle growth must remain at or below 64 handles;
- every execution must return the normal Runner success code.

This probe is deliberately isolated from parallel xUnit collections so unrelated tests do not invalidate the process-level resource measurements.

## Published-binary lifecycle soak

`build\soak-runner.ps1` exercises the self-contained `skeletonkey.exe`, not a test-only host.

Default local acceptance executes:

- 3 warm-up process runs;
- 200 minimal core workflow process runs;
- at least 30 real headless Chromium workflow process runs;
- at least 5 minutes of published-binary soak activity, continuing Chromium lifecycle churn when the minimum duration has not yet elapsed.

Each published-binary run records duration, peak Runner working set, and observed Runner handle count. The first and last sample windows are compared after warm-up.

Default fail thresholds are:

```text
maximum per-process peak Runner working set: 768 MiB
maximum first-to-last median Runner working-set growth: 192 MiB
maximum first-to-last median Runner handle growth: 96
remaining Playwright Chromium processes after cleanup: 0
```

The limits are intentionally broad enough for Windows/CI variance while still detecting runaway growth or lifecycle failures. Tightening them later requires observed production telemetry rather than guesswork.

## Browser cleanup

The browser soak uses an ephemeral headless Chromium `web.page` resource and a data URL, so it has no dependency on an external website. After the final run, Phase 28 waits for Playwright Chromium processes using the remote-debugging pipe to exit. It never kills unrelated user browser processes. Any new Playwright Chromium process that remains after the cleanup deadline fails the phase.

## Report

A successful local run creates:

```text
artifacts\soak\phase-028-soak-report.json
```

The report records the acceptance thresholds, execution counts, timing, working-set statistics, handle statistics, and orphan-browser result.

## Clean-machine scheduled gate

`.github\workflows\phase-028-soak-gate.yml` runs the clean-machine soak on `windows-2022` by manual dispatch and once per week. It uses 150 core iterations, at least 20 browser iterations, and a 10-minute minimum soak window to exercise repeated browser creation/cleanup on hosted infrastructure.

Phase 27 remains the normal push/pull-request supply-chain gate. Phase 28 is deliberately not run for every small source change because it is a longer-duration reliability gate.

## What Phase 28 does not prove

A green Phase 28 is strong single-machine stability evidence, but it is not equivalent to months of production telemetry. It does not replace:

- production-trusted Authenticode signing;
- install/update/canary validation on representative customer machines;
- final workflow/locator/checkpoint/plugin compatibility and support policy;
- monitoring of real Agent workloads after release.
