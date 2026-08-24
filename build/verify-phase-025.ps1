param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 25 verification must run on Windows."
    }

    & (Join-Path $PSScriptRoot "verify-phase-024.ps1") -RuntimeIdentifier $RuntimeIdentifier
    if ($LASTEXITCODE -ne 0) { throw "Phase 24 regression gate failed." }

    & (Join-Path $PSScriptRoot "security-audit.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Production vulnerability gate failed." }

    $selfContained = Join-Path $repo "artifacts/runner/$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "verify-release-package.ps1") $selfContained -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version -RequireSelfContained
    if ($LASTEXITCODE -ne 0) { throw "Self-contained release integrity gate failed." }

    $runner = Join-Path $selfContained "skeletonkey.exe"
    $endWorkflow = Join-Path $repo "tests/fixtures/validation/valid-minimal.workflow.json"
    & $runner run --file $endWorkflow --execution-id phase-025-core-end-smoke
    if ($LASTEXITCODE -ne 0) { throw "Published runner core.end smoke failed." }

    $versionOutput = & $runner version
    if ($LASTEXITCODE -ne 0) { throw "Published runner version command failed." }
    $versionEnvelope = ($versionOutput | Out-String) | ConvertFrom-Json
    $informationalVersion = [string]$versionEnvelope.result.informationalVersion
    $versionMatches = $informationalVersion -eq $Version -or $informationalVersion.StartsWith("$Version+", [StringComparison]::Ordinal)
    if (-not $versionMatches) {
        throw "Unexpected production informational version: $informationalVersion; expected $Version or $Version+metadata."
    }

    & (Join-Path $PSScriptRoot "package-release.ps1") $selfContained -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if ($LASTEXITCODE -ne 0) { throw "Release archive packaging failed." }

    $releaseZip = Join-Path $repo "artifacts/release/skeletonkey-$Version-$RuntimeIdentifier.zip"
    $releaseHash = "$releaseZip.sha256"
    if (-not (Test-Path -LiteralPath $releaseZip -PathType Leaf)) { throw "Release ZIP was not created." }
    if (-not (Test-Path -LiteralPath $releaseHash -PathType Leaf)) { throw "Release ZIP checksum was not created." }

    Write-Host "Phase 0-25 production release verification passed."
}
finally {
    Pop-Location
}
