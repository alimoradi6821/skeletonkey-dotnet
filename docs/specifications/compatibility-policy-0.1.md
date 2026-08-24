# Compatibility Policy 0.1

SkeletonKey product version `0.1.0` freezes the first supported compatibility surface without declaring the workflow language itself stable at 1.0.

## Frozen identities

| Contract | Supported identity |
| --- | --- |
| Workflow language | `0.1.0` |
| Workflow JSON Schema | `https://schemas.skeletonkey.dev/workflow/0.1/schema.json` |
| Locator document | `0.1.0` |
| Locator JSON Schema | `https://schemas.skeletonkey.dev/locators/0.1/schema.json` |
| Checkpoint current format | `0.3` |
| Checkpoint readable legacy formats | `0.2`, `0.1` |
| Local plugin manifest | `0.1` |
| Agent runtime bundle | `0.1` |

## Patch-release rule

A `0.1.x` patch may fix implementation defects, improve diagnostics, harden persistence, or add backward-compatible behavior. It must not silently invalidate documents/packages accepted by `0.1.0`, reuse an existing format version for incompatible semantics, or automatically replay an execution whose side-effect state is ambiguous.

## Failure semantics

Stable failure codes used by persistence and recovery are part of the host integration contract. In particular, checkpoint corruption (`SKR3003`), checkpoint storage failure (`SKR3005`), interrupted in-flight recovery (`SKR3006`), artifact persistence failure (`SKR2029`), and plugin hash mismatch (`SKP2205`) must remain fail-closed in the `0.1.x` line.
