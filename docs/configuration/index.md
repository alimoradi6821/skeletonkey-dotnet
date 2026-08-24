# Configuration

SkeletonKey intentionally keeps runtime configuration explicit. Workflow behavior lives in workflow JSON, locator catalogs, plugin manifests, and runner command-line arguments rather than hidden process configuration.

## Runtime configuration

The standalone `skeletonkey` runner does **not** currently require any SkeletonKey-specific environment variable for normal workflow execution.

Use CLI arguments for runtime paths and execution identity, including:

- `--workflow-directory`
- `--locator-directory`
- `--plugin-directory`
- `--inputs` / `--inputs-file`
- `--execution-id`
- `--checkpoint-directory`
- `--browser`
- `--format`
- `--diagnostics`

## Environment variables

The repository does use a small number of environment variables for integration tests, release signing, provenance generation, and CI behavior. They are documented in [Environment Variables](environment-variables.md).
