param(
    [switch] $SkipBrowserInstall,
    [switch] $SkipAdvancedSmoke,
    [string] $RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    dotnet restore SkeletonKey.sln
    if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
    dotnet build SkeletonKey.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Release build failed." }
    dotnet test SkeletonKey.sln --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Release tests failed." }
    dotnet format SkeletonKey.sln --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Format verification failed." }

    if (-not $SkipBrowserInstall) {
        & (Join-Path $PSScriptRoot "install-playwright-browsers.ps1") chromium
        if ($LASTEXITCODE -ne 0) { throw "Browser installation failed." }
    }

    if (-not $SkipAdvancedSmoke) {
        $env:SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE = "1"
        dotnet test tests/SkeletonKey.Web.Advanced.Integration.Tests/SkeletonKey.Web.Advanced.Integration.Tests.csproj --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) { throw "Advanced Chromium smoke failed." }
    }

    $frameworkDependent = Join-Path $repo "artifacts/runner/$RuntimeIdentifier-framework-dependent"
    & (Join-Path $PSScriptRoot "publish-runner.ps1") $RuntimeIdentifier $frameworkDependent
    $runnerDll = Join-Path $frameworkDependent "skeletonkey.dll"
    & dotnet $runnerDll version
    if ($LASTEXITCODE -ne 0) { throw "Framework-dependent DLL smoke failed." }

    $checkpointRoot = Join-Path $repo "artifacts/checkpoints/phase-019-smoke"
    if (Test-Path $checkpointRoot) { Remove-Item $checkpointRoot -Recurse -Force }
    $workflow = Join-Path $repo "tests/fixtures/conformance/valid/core-return.workflow.json"
    & dotnet $runnerDll run --file $workflow --execution-id phase-019-smoke --checkpoint-directory $checkpointRoot
    if ($LASTEXITCODE -ne 0) { throw "Durable checkpoint run smoke failed." }
    & dotnet $runnerDll resume --file $workflow --execution-id phase-019-smoke --checkpoint-directory $checkpointRoot
    if ($LASTEXITCODE -ne 0) { throw "Durable checkpoint resume smoke failed." }

    $selfContained = Join-Path $repo "artifacts/runner/$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "publish-runner.ps1") $RuntimeIdentifier $selfContained -SelfContained
    & (Join-Path $selfContained "skeletonkey.exe") version
    if ($LASTEXITCODE -ne 0) { throw "Self-contained apphost smoke failed. Run this verification on clean external Windows when the local host has the known CET/coreclr failure." }
}
finally {
    Remove-Item Env:SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE -ErrorAction SilentlyContinue
    Pop-Location
}
