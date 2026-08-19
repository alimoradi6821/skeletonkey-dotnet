# Phase 0-22 Explicit Local Plugin Loading

Status: implemented; repository verification required before release tagging.

Phase 0-22 adds an explicit host-owned plugin boundary. `ISkeletonKeyPlugin` contributes immutable node definitions, exact node handlers, and runtime resource providers. The Runner accepts repeatable `--plugin-directory` options, reads only top-level `*.skeletonkey-plugin.json` files, validates closed manifests and SHA-256 integrity, activates only the exact declared public entry type, and composes validated contributions with built-ins.

Discovery is deterministic and bounded: at most 8 directories, 64 manifests, 64 KiB per manifest, and 32 MiB per root assembly. Contributions also have fixed count limits. Assemblies and directories that are reparse points are rejected. Plugin identifiers, versions, entry types, assembly filenames, hashes, contribution namespaces, exact definition/handler pairing, and resource-provider conflicts are validated before workflow execution.

Loading a plugin executes local code. SHA-256 proves that the selected bytes match the manifest; it does not establish publisher trust or create a sandbox. The host must explicitly opt in to every local directory.

This phase does not add recursive scanning, remote discovery, package feeds, downloads, dependency injection, custom dependency probing, unloadable isolation contexts, signatures or publisher trust, permissions, process isolation, hot reload, or a security sandbox.
