# SkeletonKey 0.1.0 Support Policy

## Supported GA host

SkeletonKey `0.1.0` supports the verified self-contained Windows x64 Runner and SellerBot Agent process-boundary integration model.

- OS architecture: Windows x64
- Runner target: `net10.0-windows`
- Distribution: self-contained `win-x64`
- Browser engine: Playwright Chromium provisioned through the supported installer path
- Desktop automation: interactive Windows UI Automation session through FlaUI UIA3

Hosted/non-interactive CI does not replace the mandatory interactive desktop acceptance run.

## Contract freeze for 0.1.x

The following are compatibility contracts for the `0.1.x` line:

- Workflow specification: `0.1.0`
- Workflow schema URI: `https://schemas.skeletonkey.dev/workflow/0.1/schema.json`
- Locator specification: `0.1.0`
- Locator schema URI: `https://schemas.skeletonkey.dev/locators/0.1/schema.json`
- Current checkpoint format: `0.3`
- Accepted legacy checkpoint formats: `0.2`, `0.1`
- Local plugin manifest schema: `0.1`
- Agent runtime bundle format: `0.1`

Patch releases must not silently change these format identities or reinterpret already-valid documents in a breaking way. A breaking contract change requires a new explicit format/specification version.

## Out of scope for 0.1.0

The GA support contract does not promise remote plugin registries, sandboxed untrusted plugin execution, distributed scheduling, durable parallel-frontier migration, persistent Playwright profile resume, pending-dialog resume, or desktop-handle resume.

## Security and signing

Package manifest/SHA-256, plugin hash verification, SBOM, and provenance are mandatory release evidence. A production-trusted Authenticode certificate and timestamp are required for public signed distribution; signing secrets are never stored in the repository.
