param(
    [switch] $SkipBrowserInstall,
    [switch] $SkipAdvancedSmoke,
    [switch] $SkipDesktopSmoke,
    [string] $RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 24 verification must run on Windows."
    }

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
        $env:SKELETONKEY_PLAYWRIGHT_SMOKE = "1"
        $env:SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE = "1"
        dotnet test tests/SkeletonKey.Web.Integration.Tests/SkeletonKey.Web.Integration.Tests.csproj --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) { throw "Essential Chromium workflow smoke failed." }
        dotnet test tests/SkeletonKey.Web.Advanced.Integration.Tests/SkeletonKey.Web.Advanced.Integration.Tests.csproj --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) { throw "Advanced Chromium, network interception, and page recovery smoke failed." }
    }

    $frameworkDependent = Join-Path $repo "artifacts/runner/$RuntimeIdentifier-framework-dependent"
    & (Join-Path $PSScriptRoot "publish-runner.ps1") $RuntimeIdentifier $frameworkDependent
    $runnerDll = Join-Path $frameworkDependent "skeletonkey.dll"
    & dotnet $runnerDll version
    if ($LASTEXITCODE -ne 0) { throw "Framework-dependent DLL smoke failed." }

    $pluginRoot = Join-Path $repo "artifacts/plugins/phase-024-smoke"
    if (Test-Path $pluginRoot) { Remove-Item $pluginRoot -Recurse -Force }
    New-Item $pluginRoot -ItemType Directory | Out-Null
    $pluginSource = Join-Path $repo "tests/SkeletonKey.Runner.Core.Tests/bin/Release/net10.0-windows/SkeletonKey.Runner.Core.Tests.dll"
    $pluginAssembly = Join-Path $pluginRoot "SkeletonKey.Runner.Core.Tests.dll"
    Copy-Item $pluginSource $pluginAssembly
    $pluginHash = (Get-FileHash $pluginAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
    $pluginManifest = [ordered]@{
        schemaVersion = "0.1"
        id = "phase22.fixture"
        version = "1.0.0"
        assembly = "SkeletonKey.Runner.Core.Tests.dll"
        entryType = "SkeletonKey.Runner.Core.Tests.Phase22FixturePlugin"
        sha256 = $pluginHash
    }
    $pluginManifest | ConvertTo-Json | Set-Content (Join-Path $pluginRoot "phase22.fixture.skeletonkey-plugin.json") -Encoding utf8
    & dotnet $runnerDll plugins --plugin-directory $pluginRoot
    if ($LASTEXITCODE -ne 0) { throw "Explicit plugin inventory smoke failed." }
    $pluginWorkflow = Join-Path $repo "tests/fixtures/plugins/phase-022-echo.workflow.json"
    & dotnet $runnerDll run --file $pluginWorkflow --plugin-directory $pluginRoot --execution-id phase-024-plugin-smoke
    if ($LASTEXITCODE -ne 0) { throw "Explicit plugin execution smoke failed." }

    $dependencyRoot = Join-Path $repo "artifacts/workflows/phase-024-smoke"
    if (Test-Path $dependencyRoot) { Remove-Item $dependencyRoot -Recurse -Force }
    New-Item $dependencyRoot -ItemType Directory | Out-Null
    $childSource = Join-Path $repo "tests/fixtures/conformance/valid/core-return.workflow.json"
    $childTarget = Join-Path $dependencyRoot "child-workflow@1.0.0.workflow.json"
    (Get-Content $childSource -Raw).Replace('"id": "core-return"', '"id": "child-workflow"') | Set-Content $childTarget -Encoding utf8
    $invokeWorkflow = Join-Path $repo "tests/fixtures/conformance/valid/invoke-workflow-forward-streams.workflow.json"
    & dotnet $runnerDll analyze --file $invokeWorkflow --workflow-directory $dependencyRoot
    if ($LASTEXITCODE -ne 0) { throw "Cross-workflow dependency analysis smoke failed." }
    & dotnet $runnerDll run --file $invokeWorkflow --workflow-directory $dependencyRoot --execution-id phase-024-invocation-smoke
    if ($LASTEXITCODE -ne 0) { throw "Cross-workflow invocation smoke failed." }

    $checkpointRoot = Join-Path $repo "artifacts/checkpoints/phase-024-smoke"
    if (Test-Path $checkpointRoot) { Remove-Item $checkpointRoot -Recurse -Force }
    $workflow = Join-Path $repo "tests/fixtures/conformance/valid/core-return.workflow.json"
    & dotnet $runnerDll run --file $workflow --execution-id phase-024-checkpoint-smoke --checkpoint-directory $checkpointRoot
    if ($LASTEXITCODE -ne 0) { throw "Durable checkpoint run smoke failed." }
    & dotnet $runnerDll resume --file $workflow --execution-id phase-024-checkpoint-smoke --checkpoint-directory $checkpointRoot
    if ($LASTEXITCODE -ne 0) { throw "Durable checkpoint resume failed." }

    if (-not $SkipDesktopSmoke) {
        if (-not [Environment]::UserInteractive) {
            throw "Desktop smoke requires an interactive Windows session."
        }

        $desktopDirectory = Join-Path $repo "tests/fixtures/desktop"
        $desktopWorkflow = Join-Path $desktopDirectory "phase-023-notepad.workflow.json"
        & dotnet $runnerDll analyze --file $desktopWorkflow --locator-directory $desktopDirectory
        if ($LASTEXITCODE -ne 0) { throw "Desktop workflow and locator analysis smoke failed." }
        & dotnet $runnerDll run --file $desktopWorkflow --locator-directory $desktopDirectory --execution-id phase-024-desktop-smoke
        if ($LASTEXITCODE -ne 0) { throw "FlaUI Notepad execution smoke failed." }
    }

    $selfContained = Join-Path $repo "artifacts/runner/$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "publish-runner.ps1") $RuntimeIdentifier $selfContained -SelfContained
    & (Join-Path $selfContained "skeletonkey.exe") version
    if ($LASTEXITCODE -ne 0) { throw "Self-contained apphost smoke failed. Run this verification on clean external Windows when the local host has the known CET/coreclr failure." }
}
finally {
    Remove-Item Env:SKELETONKEY_PLAYWRIGHT_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:SKELETONKEY_PLAYWRIGHT_ADVANCED_SMOKE -ErrorAction SilentlyContinue
    Pop-Location
}
