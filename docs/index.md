# SkeletonKey Documentation

SkeletonKey 0.1.0 is a standalone Windows automation engine for graph-based workflows, browser automation through Playwright, and Windows desktop automation through FlaUI UIA3.

This documentation site is built with DocFX and includes the maintained Markdown documentation plus generated .NET API reference pages.

## Start here

- [Getting Started](getting-started.md) — build, validate, run, and resume workflows.
- [Configuration](configuration/index.md) — host configuration and environment variables.
- [Architecture Decision Records](architecture/index.md) — architectural choices and their rationale.
- [Specifications](specifications/index.md) — the versioned 0.1 contracts and behavior.
- [Development and Release](development/index.md) — verification gates, release process, and support policy.
- API Reference — generated from the public .NET surface under `src/`.

## Documentation commands

Restore the pinned documentation tool:

```powershell
dotnet tool restore
```

Build the documentation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-docs.ps1
```

Build and serve it locally:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\serve-docs.ps1
```

The generated static website is written to `artifacts/docs/site`.
