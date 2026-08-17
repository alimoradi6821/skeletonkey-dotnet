# Phase 0-16 Verification

Phase 0-16 repository implementation is complete. Release acceptance requires one successful run of the normative verification script on clean Windows with .NET SDK 10.0.302 and Playwright browser installation available.

Run from the repository root:

```powershell
build\verify-phase-016.ps1
```

The script must complete all of these gates:

- solution restore;
- Release build without restore;
- Release tests without rebuild;
- formatting verification;
- Chromium installation;
- opt-in Advanced Web Chromium integration smoke;
- framework-dependent `skeletonkey.dll version` smoke;
- generation of `manifest.json` and `SHA256SUMS`;
- self-contained `win-x64` `skeletonkey.exe version` smoke.

The original Windows host produced a machine-specific `coreclr.dll`/CET failure for apphosts, including a minimal .NET application. Do not change CET, CFG, or Exploit Protection. The required apphost gate must run on clean external Windows, Windows Sandbox, a VM, or Windows CI. A failed or skipped apphost gate means release acceptance remains pending even when repository-controlled checks pass.

Phase 0-17 must not begin until the command above has produced successful evidence for every gate.
