# Phase 0-25 Production RC Verification

Phase 25 closes the first real-world pilot gap and introduces the first production release gate.

The real browser pilot proved navigation, locator resolution, text/attribute extraction, click, and page navigation, but exposed that `core.end` was present in the built-in catalog without an exact runtime handler. Phase 25 makes the structural terminal executable and protects it with unit, default-runtime integration, published-runner, and Chromium-backed regression coverage.

Phase 25 also establishes release candidate identity `0.1.0-rc.1` and adds release-package integrity and NuGet vulnerability gates.

Run from an interactive Windows session:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-025.ps1
```

Acceptance requires:

- every Phase 0-24 acceptance test and smoke remains green with no skip switches;
- `core.end@1` resolves to an exact built-in runtime handler;
- a start-to-end workflow completes successfully without a synthetic `core.return` outcome;
- the Chromium smoke suite executes a browser-backed workflow that terminates through `core.end`;
- the self-contained published `skeletonkey.exe` executes the minimal start-to-end workflow successfully;
- `skeletonkey.exe version` reports informational version beginning with `0.1.0-rc.1`;
- `dotnet list ... --vulnerable --include-transitive` reports no vulnerable NuGet packages;
- every release payload file is represented by `manifest.json` and `SHA256SUMS`, and all byte counts and SHA-256 values match;
- a release ZIP and external SHA-256 checksum are produced under `artifacts/release`.

Successful acceptance produces:

```text
artifacts/release/skeletonkey-0.1.0-rc.1-win-x64.zip
artifacts/release/skeletonkey-0.1.0-rc.1-win-x64.zip.sha256
```

Phase 25 is the first release-candidate gate. Code signing, SBOM/provenance, clean-machine CI/canary validation, and long-duration soak testing remain subsequent production-hardening work.
