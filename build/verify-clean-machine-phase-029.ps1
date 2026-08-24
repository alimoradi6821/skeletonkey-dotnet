param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 29 clean-machine verification must run on Windows."
    }

    & (Join-Path $PSScriptRoot "verify-clean-machine-phase-028.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 28 clean-machine regression gate failed." }

    $published = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "prepare-agent-bundle.ps1") $published -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Clean-machine Agent bundle preparation failed." }

    $archive = Join-Path $repo "artifacts\agent\skeletonkey-agent-runtime-$Version-$RuntimeIdentifier.zip"
    & (Join-Path $PSScriptRoot "agent-canary.ps1") $archive -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Clean-machine Phase 29 canary failed." }

    Write-Host "Phase 29 clean-machine Agent deployment and recovery gate passed."
}
finally {
    Pop-Location
}
