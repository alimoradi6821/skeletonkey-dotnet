param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 29 verification must run on Windows."
    }

    & (Join-Path $PSScriptRoot "verify-phase-028.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 28 regression gate failed." }

    $published = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "prepare-agent-bundle.ps1") $published -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 29 Agent bundle preparation failed." }

    $archive = Join-Path $repo "artifacts\agent\skeletonkey-agent-runtime-$Version-$RuntimeIdentifier.zip"
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw "Phase 29 Agent bundle archive was not created." }

    & (Join-Path $PSScriptRoot "agent-canary.ps1") $archive -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 29 deployment/canary gate failed." }

    $report = Join-Path $repo "artifacts\canary\phase-029-canary-report.json"
    if (-not (Test-Path -LiteralPath $report -PathType Leaf)) { throw "Phase 29 canary report was not generated." }
    $json = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($json.phase -ne 29 -or $json.status -ne "passed") { throw "Phase 29 canary report is not passing." }

    Write-Host "Phase 0-29 deployment, Agent integration, canary, rollback, and crash-recovery verification passed."
}
finally {
    Pop-Location
}
