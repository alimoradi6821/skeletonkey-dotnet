param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 26 verification must run on Windows."
    }

    # Local acceptance remains the strongest gate and includes the real interactive FlaUI/Notepad smoke.
    & (Join-Path $PSScriptRoot "verify-phase-025.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 25 production regression gate failed." }

    $selfContained = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "fault-injection.ps1") $selfContained -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version
    if (-not $?) { throw "Phase 26 published-binary fault injection failed." }

    $workflowPath = Join-Path $repo ".github\workflows\phase-026-production-gate.yml"
    if (-not (Test-Path -LiteralPath $workflowPath -PathType Leaf)) {
        throw "Phase 26 clean-machine CI workflow is missing."
    }

    Write-Host "Phase 0-26 clean-machine and fault-injection verification passed."
}
finally {
    Pop-Location
}
