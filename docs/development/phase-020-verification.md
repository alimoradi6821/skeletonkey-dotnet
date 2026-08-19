# Phase 0-20 Verification

Phase 0-20 is accepted only when the full Windows verification script succeeds without exclusions.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-020.ps1
```

The script verifies restore, Release build, the complete test suite, formatting, advanced Chromium behavior, durable run/resume compatibility, cross-workflow dependency analysis and execution through an exact versioned directory registration, framework-dependent Runner packaging, and self-contained `win-x64` startup.

The complete suite must include Phase 0-20 assertions for reachable dependency resolution, missing dependencies, exact-version lookup, resolved identity, invocation cycles, maximum depth, required and unknown child inputs, static input types, mapped child stream sources, runtime preflight, and Runner `--workflow-directory` integration.

Acceptance requires:

- all tests pass;
- `dotnet format --verify-no-changes` passes;
- the versioned child dependency smoke passes for both analysis and execution;
- advanced Chromium tests pass when not explicitly skipped for diagnosis;
- checkpoint run and resume remain successful;
- both Runner package modes start successfully;
- no CET, CFG, Exploit Protection, validation, or security control is weakened.

Phase 0-20 does not claim remote workflow discovery, child resource compatibility, plugin loading, or distributed scheduling.
