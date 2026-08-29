# Schemas

This directory contains local normative schema artifacts for SkeletonKey language and host-contract versions.

## Available Versions

| Language / contract | Version | Local path | Public URI | Draft |
| --- | --- | --- | --- | --- |
| Workflow | 0.1.0 | `workflow/0.1/schema.json` | `https://schemas.skeletonkey.dev/workflow/0.1/schema.json` | JSON Schema Draft 2020-12 |
| Locators | 0.1.0 | `locators/0.1/schema.json` | `https://schemas.skeletonkey.dev/locators/0.1/schema.json` | JSON Schema Draft 2020-12 |
| Standalone execution settings | 0.1 | `standalone/0.1/schema.json` | `https://schemas.skeletonkey.dev/standalone/0.1/schema.json` | JSON Schema Draft 2020-12 |

During pre-alpha development, the local files in this repository are authoritative. Pre-1.0 language and host-contract artifacts may evolve before their public support boundary is frozen.

Public hosting for `https://schemas.skeletonkey.dev/` is not yet implemented.

Workflow 0.1 includes invocation, structured binding, expression wrapper, resource and locator reference wrappers, control-flow, iteration, early-return, and human-interaction schema support. Expression semantics, control-flow execution, resource resolution, locator resolution, browser automation, human-interaction execution, execution planning, cross-workflow dependency validation, and standalone application scheduling remain outside the workflow schema.
