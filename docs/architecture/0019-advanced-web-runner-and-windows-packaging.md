# Phase 0-16 Advanced Web, Runner, and Windows Packaging

Status: implemented; repository verification required before release tagging.

Phase 0-16 extends the provider-neutral Web contracts with multiple pages, popups, nested frame targeting, uploads, downloads, dialogs, cookies, storage-state transfer, and advanced waits. Playwright implements these contracts without exposing Playwright objects through public workflow or handler boundaries.

Workflow artifacts are written through `IWorkflowArtifactStore`. The filesystem implementation owns one canonical root, rejects absolute paths, traversal, separators, Windows device names, invalid filenames, and size overflow, and returns opaque artifact references with SHA-256 metadata. Workflows never select host filesystem paths.

The `skeletonkey` runner provides `version`, `validate`, `analyze`, `plan`, `run`, and `install-browsers`. Commands return deterministic JSON envelopes and stable process exit codes. `--format ndjson` emits ordered runtime event records followed by exactly one result record. `--diagnostics` writes bounded single-line lifecycle diagnostics to stderr and never mixes them with stdout machine output. Ctrl+C is translated into cooperative runtime cancellation and exit code 130.

Windows packages are created by `build/publish-runner.ps1`. Every package contains `manifest.json` and `SHA256SUMS`. `build/verify-phase-016.ps1` is the normative verification entry point and covers restore, Release build, tests, formatting, Chromium installation, advanced smoke tests, framework-dependent DLL execution, and self-contained apphost execution.

The known CET/coreclr failure on the original Windows machine is an environment blocker, not a product workaround target. Security settings must not be weakened. Final release evidence must include a successful self-contained `win-x64` apphost run on clean external Windows, Windows Sandbox, a VM, or CI.

Phase 0-16 does not add persistence/resume, retry scheduling, parallel scheduling, desktop automation, plugin discovery, or arbitrary filesystem access.
