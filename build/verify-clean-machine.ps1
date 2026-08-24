param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 26 clean-machine verification must run on Windows."
    }

    # Hosted CI sessions are intentionally non-interactive. All non-desktop Phase 0-24 gates still run,
    # including Chromium integration/recovery, plugins, invocation, checkpoint/resume, and both publish modes.
    & (Join-Path $PSScriptRoot "verify-phase-024.ps1") -SkipDesktopSmoke -RuntimeIdentifier $RuntimeIdentifier
    if (-not $?) { throw "Clean-machine Phase 0-24 regression gate failed." }

    & (Join-Path $PSScriptRoot "security-audit.ps1")
    if (-not $?) { throw "Clean-machine vulnerability gate failed." }

    $selfContained = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "verify-release-package.ps1") $selfContained -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version -RequireSelfContained
    if (-not $?) { throw "Clean-machine release integrity gate failed." }

    $runner = Join-Path $selfContained "skeletonkey.exe"
    $endWorkflow = Join-Path $repo "tests\fixtures\validation\valid-minimal.workflow.json"
    & $runner run --file $endWorkflow --execution-id phase-026-clean-machine-core-end
    if ($LASTEXITCODE -ne 0) { throw "Clean-machine published core.end smoke failed." }

    & (Join-Path $PSScriptRoot "fault-injection.ps1") $selfContained -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version
    if (-not $?) { throw "Clean-machine published-binary fault injection failed." }

    & (Join-Path $PSScriptRoot "package-release.ps1") $selfContained -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Clean-machine release packaging failed." }

    Write-Host "Phase 26 clean-machine verification passed."
}
finally {
    Pop-Location
}
