# Phase 0-22 Verification

Phase 0-22 is accepted only when the full Windows verification script succeeds without exclusions.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-022.ps1
```

The script verifies restore, Release build, the complete test suite, formatting, advanced Chromium behavior, durable run/resume compatibility, cross-workflow execution, both Runner package modes, and an external-process plugin smoke. The plugin smoke copies a fixture assembly into an explicit directory, generates its closed manifest from the computed SHA-256, inventories it with `plugins`, and executes a workflow containing the contributed node.

Acceptance requires:

- all tests and formatting checks pass;
- malformed, unknown-property, hash-mismatched, and identity-mismatched packages fail with stable codes;
- valid plugin definitions, handlers, and resource providers load in deterministic order;
- Runner analysis and execution compose the explicitly supplied plugin;
- existing Web, checkpoint, invocation, and packaging smokes remain green;
- no security control is weakened.

Phase 0-22 does not claim publisher trust, digital signatures, sandboxing, process isolation, remote discovery, package feeds, recursive assembly scanning, dependency injection, hot reload, or custom dependency probing.
