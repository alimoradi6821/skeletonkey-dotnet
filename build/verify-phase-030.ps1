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
        throw "Phase 30 verification must run on Windows."
    }
    if ($Version -ne "0.1.0") { throw "Phase 30 GA is frozen to version 0.1.0." }

    [xml] $buildProps = Get-Content -LiteralPath (Join-Path $repo "Directory.Build.props") -Raw
    $versionPrefixNode = $buildProps.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")
    $versionSuffixNode = $buildProps.SelectSingleNode("/Project/PropertyGroup/VersionSuffix")
    if ($null -eq $versionPrefixNode) { throw "Directory.Build.props VersionPrefix is missing." }

    $versionPrefix = ([string] $versionPrefixNode.InnerText).Trim()
    $versionSuffix = if ($null -eq $versionSuffixNode) { "" } else { ([string] $versionSuffixNode.InnerText).Trim() }
    if ($versionPrefix -ne $Version) { throw "Directory.Build.props VersionPrefix mismatch: expected $Version, found '$versionPrefix'." }
    if (-not [string]::IsNullOrWhiteSpace($versionSuffix)) { throw "GA build must not contain VersionSuffix; found '$versionSuffix'." }

    & (Join-Path $PSScriptRoot "verify-phase-029.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 29 regression gate failed for GA version $Version." }

    $published = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"
    & (Join-Path $PSScriptRoot "ga-storage-faults.ps1") $published -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 30 storage/disk-permission failure gate failed." }

    & (Join-Path $PSScriptRoot "finalize-ga-release.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version -RequireSignedRelease:$RequireSignedRelease
    if (-not $?) { throw "Phase 30 GA artifact finalization failed." }

    $report = Join-Path $repo "artifacts\release\skeletonkey-$Version-$RuntimeIdentifier.ga.json"
    if (-not (Test-Path -LiteralPath $report -PathType Leaf)) { throw "Phase 30 GA report was not created." }

    Write-Host "Phase 0-30 Final GA verification passed."
    Write-Host "SkeletonKey $Version is GA-ready for the verified Windows $RuntimeIdentifier support contract."
}
finally {
    Pop-Location
}
