# Schemas

This directory contains local normative schema artifacts for SkeletonKey language versions.

## Available Versions

| Language | Version | Local path | Public URI | Draft |
| --- | --- | --- | --- | --- |
| Workflow | 0.1.0 | `workflow/0.1/schema.json` | `https://schemas.skeletonkey.dev/workflow/0.1/schema.json` | JSON Schema Draft 2020-12 |
| Locators | 0.1.0 | `locators/0.1/schema.json` | `https://schemas.skeletonkey.dev/locators/0.1/schema.json` | JSON Schema Draft 2020-12 |

During pre-alpha development, the local files in this repository are authoritative. Pre-1.0 language artifacts may evolve before the first public tagged release.

Public hosting for `https://schemas.skeletonkey.dev/` is not yet implemented.

Workflow 0.1 includes invocation, structured binding, expression wrapper, resource and locator reference wrappers, control-flow, iteration, early-return, and human-interaction schema support. Expression semantics, control-flow execution, resource resolution, locator resolution, browser automation, human-interaction execution, execution planning, and cross-workflow dependency validation remain outside the schema.
