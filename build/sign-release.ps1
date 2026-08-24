param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string] $PfxPath,

    [string] $PfxPassword = $env:SKELETONKEY_SIGNING_PFX_PASSWORD,

    [Parameter(Mandatory = $true)]
    [string] $TimestampServer,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$package = Resolve-Path -LiteralPath $PackageDirectory
$pfx = Resolve-Path -LiteralPath $PfxPath
$runner = Join-Path $package "skeletonkey.exe"

if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw "Published runner executable was not found: $runner"
}
if ([string]::IsNullOrWhiteSpace($PfxPassword)) {
    throw "PFX password is required. Prefer SKELETONKEY_SIGNING_PFX_PASSWORD instead of placing secrets on the command line."
}
if ([string]::IsNullOrWhiteSpace($TimestampServer)) {
    throw "A production timestamp server URL is required."
}

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $pfx.Path,
    $PfxPassword,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
)

try {
    if (-not $certificate.HasPrivateKey) {
        throw "The supplied code-signing certificate does not contain a private key."
    }

    $signature = Set-AuthenticodeSignature -FilePath $runner -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer
    if ([string]$signature.Status -ne "Valid") {
        throw "Authenticode signing did not produce a valid signature. Status: $($signature.Status); message: $($signature.StatusMessage)"
    }
}
finally {
    $certificate.Dispose()
}

& (Join-Path $PSScriptRoot "refresh-release-manifest.ps1") $package
if (-not $?) { throw "Release manifest refresh failed after signing." }

& (Join-Path $PSScriptRoot "verify-release-package.ps1") $package -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version -RequireSelfContained
if (-not $?) { throw "Signed release integrity verification failed." }

Write-Host "Authenticode signing passed for skeletonkey.exe."
Write-Host "Re-run package-release.ps1 and Phase 27 metadata generation so ZIP, SBOM/provenance, and checksums describe the signed payload."
