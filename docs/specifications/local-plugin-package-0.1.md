# Local Plugin Package 0.1

## Manifest

A package is a top-level file ending in `.skeletonkey-plugin.json`. The JSON object is closed and contains exactly these required properties:

```json
{
  "schemaVersion": "0.1",
  "id": "example.vendor",
  "version": "1.0.0",
  "assembly": "Example.Plugin.dll",
  "entryType": "Example.Plugin.EntryPoint",
  "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
}
```

`assembly` is a filename in the manifest directory, never a path. The file must be a non-empty DLL no larger than 32 MiB and its SHA-256 must match. `entryType` is resolved by exact, case-sensitive name and must be a public, concrete `ISkeletonKeyPlugin` implementation with a public parameterless constructor.

## Contributions

The implementation identity must exactly match the manifest. A plugin may contribute at most 256 definitions, 256 handlers, and 64 resource providers. Every definition requires exactly one handler with the same type and version, and every handler requires a definition. Node types and resource kinds must begin with `<plugin-id>.`. Duplicate plugin IDs, definition keys, handler keys, provider kinds, or conflicts with host built-ins fail before execution.

The current host loads root assemblies into the default load context and relies on framework and already-hosted SkeletonKey dependencies. Custom dependency probing is outside version 0.1.

## Runner

`--plugin-directory <path>` is repeatable up to 8 times on `plugins`, `analyze`, `plan`, `run`, and `resume`. `plugins` emits a deterministic JSON inventory. Semantic-only `validate` does not load executable plugins.

Stable loader failures use `SKP2201` through `SKP2210` for directory, manifest, assembly, integrity, activation, identity, definition, handler, and provider failures.
