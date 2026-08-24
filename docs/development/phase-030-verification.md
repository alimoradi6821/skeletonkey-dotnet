# Phase 30 Verification - Final GA 0.1.0

Phase 30 freezes SkeletonKey `0.1.0` as the first general-availability Windows x64 release contract.

## Acceptance

Run in an interactive Windows session:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-030.ps1
```

The gate reruns Phase 0-29 against the final version identity, including browser, desktop, plugins, checkpoint/resume, fault injection, SBOM/provenance, soak, Agent blue/green promotion, rollback, and crash recovery. It then verifies the two GA storage-failure contracts:

- unavailable checkpoint persistence fails with `SKR3005`;
- filesystem artifact persistence failure maps to `SKR2029`.

The build must report `0.1.0` or `0.1.0+<source metadata>` and must not contain a prerelease suffix.

## GA artifacts

A successful run produces:

```text
artifacts\release\skeletonkey-0.1.0-win-x64.zip
artifacts\release\skeletonkey-0.1.0-win-x64.zip.sha256
artifacts\release\skeletonkey-0.1.0-win-x64.sbom.cdx.json
artifacts\release\skeletonkey-0.1.0-win-x64.provenance.json
artifacts\release\skeletonkey-0.1.0-win-x64.signing-readiness.json
artifacts\release\skeletonkey-0.1.0-win-x64.ga.json
artifacts\agent\skeletonkey-agent-runtime-0.1.0-win-x64.zip
artifacts\agent\skeletonkey-agent-runtime-0.1.0-win-x64.zip.sha256
```

## Authenticode

Repository verification can complete with signing state `ready-unsigned` because the production certificate is an external credential, not source material. Public production distribution should sign `skeletonkey.exe` with a production-trusted code-signing certificate and timestamp service, regenerate the release archive/SBOM/provenance, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-030.ps1 -RequireSignedRelease
```

No development or self-signed certificate is accepted as a substitute for production trust.
