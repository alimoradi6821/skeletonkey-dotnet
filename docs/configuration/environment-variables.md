# Environment Variables

This page is the maintained inventory of environment variables read or set by the SkeletonKey repository.

> [!IMPORTANT]
> Normal SkeletonKey 0.1.0 workflow execution does not require a project-specific environment variable. Runtime behavior should be configured through workflow documents and runner arguments unless a future specification explicitly introduces an environment contract.

## SkeletonKey variables

| Variable | Scope | Required | Value | Purpose |
| --- | --- | --- | --- | --- |
| `SKELETONKEY_PLAYWRIGHT_SMOKE` | Tests / verification | No | `1` enables | Enables the essential real-Chromium integration smoke tests. Verification scripts set it automatically when those tests are expected to run. |
| `SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE` | Tests / verification | No | `1` enables | Enables the advanced Chromium integration tests, including advanced browser and network-interception acceptance paths. Verification scripts set it automatically. |
| `SKELETONKEY_SIGNING_PFX_PASSWORD` | Release signing | Only when `-PfxPassword` is omitted | Secret string | Default password source for `build/sign-release.ps1`. Do not commit this value or print it in logs. |

### Test smoke variables

You normally do not need to set the Playwright smoke variables manually because the phase verification scripts manage them. For an explicit test invocation:

```powershell
$env:SKELETONKEY_PLAYWRIGHT_SMOKE = "1"
$env:SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE = "1"
```

Remove them from the current process when finished:

```powershell
Remove-Item Env:SKELETONKEY_PLAYWRIGHT_SMOKE -ErrorAction SilentlyContinue
Remove-Item Env:SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE -ErrorAction SilentlyContinue
```

### Signing password

Prefer a secret store or CI secret injection. For a local signing session:

```powershell
$env:SKELETONKEY_SIGNING_PFX_PASSWORD = "<secret>"
```

Clear it from the process after signing:

```powershell
Remove-Item Env:SKELETONKEY_SIGNING_PFX_PASSWORD -ErrorAction SilentlyContinue
```

The signing script also accepts the password as an explicit parameter. The environment variable is only a fallback input.

## GitHub Actions provenance variables

`build/generate-provenance.ps1` consumes the following variables when it is running inside GitHub Actions. GitHub provides these values; users should not normally set them manually.

| Variable | Source | Purpose |
| --- | --- | --- |
| `GITHUB_RUN_ID` | GitHub Actions | Detects an Actions build and contributes to builder/invocation identity. |
| `GITHUB_RUN_ATTEMPT` | GitHub Actions | Distinguishes retries of the same workflow run. |
| `GITHUB_SERVER_URL` | GitHub Actions | Forms the provenance builder URL. Falls back to `https://github.com` when unavailable. |
| `GITHUB_REPOSITORY` | GitHub Actions | Identifies the repository in the provenance builder ID. |

When `GITHUB_RUN_ID` is absent, provenance generation uses its local-build identity rather than pretending to be a GitHub Actions build.

## CI and .NET CLI variables

The Windows verification workflows define these standard variables:

| Variable | Value in CI | Purpose |
| --- | --- | --- |
| `CI` | `true` | Enables deterministic CI-specific MSBuild behavior through `Directory.Build.props`. |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | `1` | Disables .NET CLI telemetry for verification jobs. |
| `DOTNET_NOLOGO` | `1` | Suppresses the .NET CLI banner in CI logs. |

These are build/CI controls, not SkeletonKey workflow runtime settings.

## Adding a new environment variable

A new project-specific environment variable must not be introduced silently. When one is needed:

1. Document its exact name, accepted values, default, scope, and security sensitivity on this page.
2. Prefer explicit CLI/workflow configuration when the setting affects workflow semantics.
3. Add validation for malformed values rather than silently accepting arbitrary strings.
4. Never store credentials or private keys in source-controlled configuration.
5. Record an ADR when the variable introduces a new cross-process or operational contract.
