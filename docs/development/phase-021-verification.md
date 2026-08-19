# Phase 0-21 Verification

Phase 0-21 is accepted only when the full Windows verification script succeeds without exclusions.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-021.ps1
```

The script verifies restore, Release build, the complete test suite, formatting, advanced Chromium behavior including synthetic network fulfillment and fail-closed blocking, durable run/resume compatibility, cross-workflow dependency analysis and execution through an exact versioned directory registration, framework-dependent Runner packaging, and self-contained `win-x64` startup.

The complete suite must include Phase 0-21 assertions for ordered rule matching, method and resource-type filtering, default blocking, immutable header mutation, protected-header rejection, header-line injection rejection, bounded synthetic responses, closed JSON policy parsing, provider capability declaration, and real Chromium context routing.

Acceptance requires:

- all tests pass;
- `dotnet format --verify-no-changes` passes;
- advanced Chromium interception and existing advanced Web tests pass when not explicitly skipped for diagnosis;
- the versioned child dependency smoke passes for both analysis and execution;
- checkpoint run and resume remain successful;
- both Runner package modes start successfully;
- no CET, CFG, Exploit Protection, validation, or security control is weakened.

Phase 0-21 does not claim upstream response-body rewriting, HAR support, WebSocket message routing, mutable policies, proxy configuration, network-state checkpointing, or distributed browser routing.
