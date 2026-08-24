# Production Readiness

## Current milestone

`0.1.0` GA after successful Phase 0-29 acceptance and Phase 30 compatibility/storage-failure finalization.

## Closed production gates

- full Phase 0-29 regression acceptance
- real Chromium and interactive FlaUI desktop automation acceptance
- self-contained Windows x64 packaging
- release manifest and SHA-256 verification
- top-level and transitive NuGet vulnerability audit
- destructive checkpoint/plugin/release fault injection
- CycloneDX SBOM and SLSA-compatible provenance
- code-signing readiness and production signing entry point
- clean-machine Windows CI
- in-process and published-binary resource soak
- orphan Chromium detection
- hash-closed SellerBot Agent runtime bundle
- blue/green staging, atomic promotion, and rollback
- safe-boundary crash/browser-loss resume
- interrupted in-flight fail-closed recovery with `SKR3006`
- stable checkpoint storage failure `SKR3005`
- stable artifact persistence failure `SKR2029`
- explicit `0.1.x` compatibility and support freeze
- GA release/runbook evidence record

## External operational requirement

The repository cannot contain or manufacture the organization's production code-signing identity. The normal source gate may record `ready-unsigned`. Public production distribution should supply a production-trusted Authenticode certificate and timestamp service externally, run `build/sign-release.ps1`, regenerate the archive/SBOM/provenance, and rerun Phase 30 with `-RequireSignedRelease`.

A development or self-signed certificate is not treated as production trust.

## GA support boundary

The supported host and versioned contracts are defined in [Support Policy](support-policy.md) and [Compatibility Policy 0.1](../specifications/compatibility-policy-0.1.md). The operational promotion/rollback procedure is defined in [GA Release Runbook](ga-release-runbook.md).
