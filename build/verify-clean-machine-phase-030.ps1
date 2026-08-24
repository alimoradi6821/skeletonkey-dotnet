param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0",

    [switch] $RequireSignedRelease
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 30 clean-machine verification must run on Windows."
    }
    if ($Version -ne "0.1.0") { throw "Phase 30 GA is frozen to version 0.1.0." }

    & (Join-Path $PSScriptRoot "verify-clean-machine-phase-029.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Clean-machine Phase 29 regression gate failed." }

    $published = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "ga-storage-faults.ps1") $published -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Clean-machine Phase 30 storage failure gate failed." }

    & (Join-Path $PSScriptRoot "finalize-ga-release.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version -RequireSignedRelease:$RequireSignedRelease
    if (-not $?) { throw "Clean-machine Phase 30 finalization failed." }

    Write-Host "Phase 30 clean-machine GA verification passed."
}
finally {
    Pop-Location
}
