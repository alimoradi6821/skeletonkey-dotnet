# Phase 0-23 Verification

Phase 0-23 is accepted only when the full script succeeds in an interactive Windows session without exclusions.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-023.ps1
```

The script performs restore, Release build, the complete test suite, formatting verification, advanced Chromium smoke tests, explicit plugin loading/execution, cross-workflow analysis/execution, durable checkpoint run/resume, framework-dependent and self-contained Windows publishing, and a real Notepad workflow through FlaUI UIA3.

The desktop smoke first analyzes the workflow with `--locator-directory`, then launches Notepad, fills its editor through a semantic UI Automation locator, reads the text, completes the workflow, and disposes the application resource. Run it from a normal signed-in desktop, not a non-interactive service session.

Acceptance requires:

- restore, build, all tests, and formatting pass;
- the desktop catalog has exactly one handler for each definition;
- launch and attach constraints reject unknown or contradictory values;
- the Runner resolves the external Locator Catalog consistently during analysis and execution;
- the Notepad UIA3 smoke succeeds and the launched process is closed;
- every Phase 0-22 browser, plugin, checkpoint, invocation, and packaging regression remains green;
- no security control or PowerShell execution policy is changed persistently.

`-SkipDesktopSmoke`, `-SkipAdvancedSmoke`, and `-SkipBrowserInstall` are diagnostic conveniences only. A run using any skip switch is not Phase 23 acceptance.
