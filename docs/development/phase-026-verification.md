# Phase 26 Verification — Clean-machine CI and Fault Injection

Phase 26 turns the Phase 25 release candidate gate into an external clean-Windows verification path and adds destructive fault-injection checks against the published self-contained Runner.

## Acceptance command

Run from an interactive Windows checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-026.ps1
```

The local Phase 26 gate first reruns the complete Phase 25 acceptance path, including the interactive FlaUI/Notepad smoke, and then executes published-binary fault injection.

Expected terminal message:

```text
Phase 0-26 clean-machine and fault-injection verification passed.
```

## Faults injected against `skeletonkey.exe`

1. A valid persisted checkpoint is modified so its SHA-256 no longer matches. `resume` must fail closed with `SKR3003`.
2. A valid explicit plugin assembly is paired with a deliberately false manifest SHA-256. `plugins` must fail closed with `SKP2205`.
3. One byte is appended to a payload file in a copied release directory. `verify-release-package.ps1` must reject the package.

The runner-level test suite also contains a public-command regression that tampers a checkpoint and verifies the stable `SKR3003` envelope.

## Clean-machine CI

`.github/workflows/phase-026-production-gate.yml` runs on a fresh `windows-2022` GitHub-hosted runner with .NET SDK `10.0.302`. It executes `build/verify-clean-machine.ps1`, which runs all automation-safe Phase 0-24 regression gates, Chromium integration/recovery, plugin/invocation/checkpoint smokes, both publish modes, vulnerability audit, release integrity, `core.end`, fault injection, and release packaging.

The hosted CI gate intentionally skips only the interactive FlaUI/Notepad smoke because GitHub-hosted runners are non-interactive. That desktop smoke remains mandatory in local `verify-phase-026.ps1` through the full Phase 25 regression gate.

## Release artifact

Successful clean-machine CI uploads:

```text
skeletonkey-0.1.0-rc.1-win-x64.zip
skeletonkey-0.1.0-rc.1-win-x64.zip.sha256
```

Phase 26 does not add product features or weaken any previous acceptance gate.
## PowerShell exit-code isolation

Expected negative native-process probes intentionally leave a non-zero `$LASTEXITCODE`. The Phase 26 fault gate clears that value after all assertions pass, and parent PowerShell gates use PowerShell invocation success (`$?`) for child `.ps1` scripts. Direct `skeletonkey.exe` invocations continue to be checked with `$LASTEXITCODE`. This prevents successful fault injection from being reported as a false failure.

