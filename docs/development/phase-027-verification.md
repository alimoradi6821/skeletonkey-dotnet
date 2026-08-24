# Phase 27 Verification — SBOM, Provenance, and Code-Signing Readiness

Phase 27 adds supply-chain records around the already-green Phase 26 release candidate. It does not add automation features or weaken any previous acceptance gate.

## Acceptance command

Run from an interactive Windows checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-phase-027.ps1
```

Expected terminal message:

```text
Phase 0-27 SBOM, provenance, and code-signing-readiness verification passed.
```

The local gate reruns Phase 26 first, including the full Phase 25 release-candidate regression path and the interactive FlaUI/Notepad smoke.

## Generated release records

A successful Phase 27 run produces:

```text
artifacts\release\skeletonkey-0.1.0-rc.1-win-x64.zip
artifacts\release\skeletonkey-0.1.0-rc.1-win-x64.zip.sha256
artifacts\release\skeletonkey-0.1.0-rc.1-win-x64.sbom.cdx.json
artifacts\release\skeletonkey-0.1.0-rc.1-win-x64.provenance.json
artifacts\release\skeletonkey-0.1.0-rc.1-win-x64.signing-readiness.json
```

The SBOM uses CycloneDX JSON 1.5 and is generated from the solution-wide direct and transitive NuGet package inventory. The Phase 27 verifier requires unique `bom-ref` values and a non-empty dependency inventory.

The provenance record is an in-toto Statement v1 with a SLSA provenance v1 predicate. It binds SHA-256 subjects for the release ZIP, SBOM, and signing-readiness record and records a deterministic source-tree digest plus key build inputs and SDK/runtime metadata. If a Git checkout is available, the Git commit and dirty-state are also recorded without publishing repository credentials.

## Code-signing policy

Phase 27 is a signing-readiness gate, not a substitute for a production certificate. An unsigned RC is accepted only with an explicit `ready-unsigned` record proving that the Windows Authenticode signing and verification commands are available and that the published executable has no invalid existing signature.

Production signing is performed with:

```powershell
$env:SKELETONKEY_SIGNING_PFX_PASSWORD = '<secret-from-secure-store>'
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\sign-release.ps1 `
  .\artifacts\runner\win-x64-self-contained `
  -PfxPath C:\secure\skeletonkey-code-signing.pfx `
  -TimestampServer https://<your-rfc3161-or-authenticode-timestamp-service>
```

`sign-release.ps1` signs `skeletonkey.exe` with SHA-256, requires timestamping, refreshes the package manifest/checksums after the executable changes, and re-verifies package integrity. After signing, rerun `package-release.ps1`, Phase 27 metadata generation, and `verify-supply-chain.ps1` so all hashes refer to the signed payload.

Never commit the PFX or its password. CI production signing should receive both from a protected secret/certificate store.

## Clean-machine CI

`.github/workflows/phase-027-supply-chain-gate.yml` supersedes the Phase 26 push/PR workflow and runs the Phase 27 clean-machine gate on `windows-2022`. The historical Phase 26 workflow remains manually dispatchable.
