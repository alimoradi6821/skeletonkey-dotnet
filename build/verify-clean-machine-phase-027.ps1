param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 27 clean-machine verification must run on Windows."
    }

    & (Join-Path $PSScriptRoot "verify-clean-machine.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 26 clean-machine regression gate failed." }

    $releaseBase = Join-Path $repo "artifacts\release\skeletonkey-$Version-$RuntimeIdentifier"
    $releaseZip = "$releaseBase.zip"
    $releaseChecksum = "$releaseBase.zip.sha256"
    $sbom = "$releaseBase.sbom.cdx.json"
    $signing = "$releaseBase.signing-readiness.json"
    $provenance = "$releaseBase.provenance.json"
    $published = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"

    & (Join-Path $PSScriptRoot "generate-sbom.ps1") -Version $Version -RuntimeIdentifier $RuntimeIdentifier -OutputPath $sbom
    if (-not $?) { throw "Clean-machine SBOM generation failed." }

    & (Join-Path $PSScriptRoot "generate-signing-readiness.ps1") $published -Version $Version -RuntimeIdentifier $RuntimeIdentifier -OutputPath $signing
    if (-not $?) { throw "Clean-machine signing-readiness gate failed." }

    & (Join-Path $PSScriptRoot "generate-provenance.ps1") -ReleaseZip $releaseZip -SbomPath $sbom -SigningReadinessPath $signing -Version $Version -RuntimeIdentifier $RuntimeIdentifier -OutputPath $provenance
    if (-not $?) { throw "Clean-machine provenance generation failed." }

    & (Join-Path $PSScriptRoot "verify-supply-chain.ps1") -ReleaseZip $releaseZip -ReleaseChecksum $releaseChecksum -SbomPath $sbom -ProvenancePath $provenance -SigningReadinessPath $signing -PublishedDirectory $published -Version $Version -RuntimeIdentifier $RuntimeIdentifier
    if (-not $?) { throw "Clean-machine supply-chain verification failed." }

    Write-Host "Phase 27 clean-machine supply-chain verification passed."
}
finally {
    Pop-Location
}
