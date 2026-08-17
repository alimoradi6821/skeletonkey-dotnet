# Phase 0-18 Verification

Phase 0-18 implementation is complete. Release acceptance requires one successful run of the normative verification script on clean Windows with .NET SDK 10.0.302 and Playwright Chromium available.

Run from the repository root. If local PowerShell policy blocks unsigned scripts, use the process-scoped bypass shown here:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-018.ps1
```

The script must complete all of these gates:

- solution restore;
- Release build without restore;
- all Release tests, including timeout, retry/backoff, continue/stop, retry checkpoint resume, checkpoint integrity, and no-duplicate resume tests;
- formatting verification;
- Chromium installation and opt-in Advanced Web smoke;
- framework-dependent Runner smoke;
- `run` plus `resume` durable checkpoint smoke;
- package manifest and SHA-256 checksum generation;
- self-contained `win-x64` apphost smoke.

For a faster repository-only pass when Chromium is already covered separately:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-018.ps1 -SkipBrowserInstall -SkipAdvancedSmoke
```

Skipping browser gates is useful for iteration but is not final phase acceptance. Do not weaken CET, CFG, or Exploit Protection to make an apphost pass; use clean external Windows, Windows Sandbox, a VM, or Windows CI if the original host reproduces the known environment-specific `coreclr.dll` failure.
