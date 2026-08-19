# Phase 0-24 Verification

Phase 0-24 is accepted only when the full script succeeds in an interactive Windows session without exclusions.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-024.ps1
```

The script performs restore, Release build, the complete test suite, formatting verification, advanced Chromium smoke tests including durable page reconstruction, explicit plugin loading/execution, cross-workflow analysis/execution, durable checkpoint run/resume, framework-dependent and self-contained Windows publishing, and a real Notepad workflow through FlaUI UIA3.

The Phase 24 Chromium recovery smoke deliberately stops after a safe post-navigation checkpoint, creates a fresh runtime and Playwright provider, restores browser storage/page identity and URL state, and completes the remaining locator/text step without replaying the completed navigation node.

Acceptance requires:

- restore, build, all tests, and formatting pass;
- format 0.3 resource state round-trips through the filesystem checkpoint store;
- a resumable resource is captured and reconstructed before remaining nodes execute;
- non-resumable activated resources fail closed with `SKR3008`;
- ephemeral Chromium page state is reconstructed in a fresh browser context;
- persistent profiles and pending-dialog states remain non-resumable;
- every Phase 0-23 browser, desktop, plugin, checkpoint, invocation, and packaging regression remains green;
- no security control or PowerShell execution policy is changed persistently.

`-SkipDesktopSmoke`, `-SkipAdvancedSmoke`, and `-SkipBrowserInstall` are diagnostic conveniences only. A run using any skip switch is not Phase 24 acceptance.
