# Phase 0-19 Verification

Phase 0-19 is accepted only when the full Windows verification script succeeds without exclusions.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-019.ps1
```

The script verifies restore, Release build, the complete test suite, formatting, advanced Chromium behavior, durable run/resume compatibility, framework-dependent Runner packaging, and self-contained `win-x64` startup.

The complete test suite must include the Phase 0-19 assertions that independent ready handlers overlap, the global concurrency limit is respected, parallel foreach honors its declared bound, result projection remains plan ordered, and invalid runtime limits are rejected.

Acceptance requires:

- all tests pass;
- `dotnet format --verify-no-changes` passes;
- advanced Chromium tests pass when not explicitly skipped for diagnosis;
- checkpoint run and resume remain successful;
- both Runner package modes start successfully;
- no CET, CFG, Exploit Protection, validation, or security control is weakened.

Phase 0-19 does not claim durable parallel-frontier recovery or distributed scheduling.
