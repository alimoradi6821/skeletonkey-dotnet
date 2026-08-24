# ADR 0028: Use DocFX and MADR-Style Architecture Decision Records

- Status: Accepted
- Date: 2026-08-21
- Decision makers: SkeletonKey maintainers

## Context and Problem Statement

SkeletonKey has a substantial Markdown documentation set, versioned specifications, development verification records, and a growing public .NET API. The repository needs a searchable documentation website comparable to the role MkDocs commonly fills in Python projects, while also preserving architectural rationale in a consistent ADR format.

The documentation tool must fit the .NET 10 codebase, generate API reference from C# projects, render the existing Markdown without forcing a documentation rewrite, and remain reproducible in local development and CI.

## Decision Drivers

- Native support for .NET API documentation and XML documentation comments.
- Reuse of the existing Markdown documentation tree.
- Static HTML output that is easy to host on common static-site platforms.
- Reproducible tool versioning in the repository.
- Searchable modern documentation UI.
- Low friction for future ADR creation and review in source control.

## Considered Options

- DocFX with the modern template plus a local .NET tool manifest.
- MkDocs maintained as a separate Python documentation toolchain.
- Docusaurus / Node-based documentation stack.
- Plain Markdown only, with no generated site or API reference.

## Decision Outcome

Chosen option: **DocFX with a pinned local tool and MADR-style ADRs**.

DocFX is pinned in `.config/dotnet-tools.json`, configured by `docs/docfx.json`, and generates both conceptual documentation and .NET API reference. The website uses DocFX's `default` and `modern` templates. Generated site output is written below `artifacts/docs/site`, while generated intermediate API YAML remains under the ignored `docs/api` directory.

Existing architectural records remain in `docs/architecture` to preserve stable links. New records continue the existing four-digit sequence and use `docs/architecture/adr-template.md`, which adopts the core MADR concepts of explicit context, decision drivers, considered options, outcome, consequences, and confirmation.

Environment variables used by the repository are maintained as an explicit configuration inventory under `docs/configuration/environment-variables.md`.

### Consequences

- The documentation stack stays aligned with the .NET ecosystem and can generate API pages from C# source projects.
- Existing Markdown specifications and development records become part of one searchable static site.
- Contributors restore the exact DocFX version with `dotnet tool restore`; no global DocFX install is required.
- Documentation generation requires a compatible .NET SDK. The repository's Windows-targeted projects make the Windows documentation CI job the authoritative API-reference build environment.
- Generated API YAML and generated HTML are build artifacts and are not source-controlled.
- Existing ADRs are not rewritten into the new template; only future ADRs are expected to follow it.

## Confirmation

The decision is confirmed by:

- `.config/dotnet-tools.json` pinning DocFX.
- `docs/docfx.json` defining the documentation and API build.
- `build/verify-docs.ps1` producing and validating the static site.
- `.github/workflows/documentation.yml` building the site on Windows CI.
- `docs/architecture/adr-template.md` defining the maintained ADR format.
- `docs/configuration/environment-variables.md` maintaining the environment-variable inventory.

## Pros and Cons of the Options

### DocFX

- Good, because it natively generates .NET API documentation.
- Good, because it renders the existing Markdown tree and produces static output.
- Good, because it is distributed as a .NET tool and can be pinned locally.
- Neutral, because documentation CI is best run on Windows for this repository's complete project graph.

### MkDocs

- Good, because it is mature and familiar in Python ecosystems.
- Bad, because it introduces a separate Python documentation toolchain for a .NET-native project.
- Bad, because C# API generation would require additional integration rather than being the native path.

### Docusaurus

- Good, because it provides a capable documentation website experience.
- Bad, because it introduces Node tooling and does not provide the same direct .NET API metadata path.

### Plain Markdown

- Good, because it has no additional tool dependency.
- Bad, because it does not provide generated API reference, site search, or a cohesive documentation website.
