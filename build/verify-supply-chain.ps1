param(
    [Parameter(Mandatory = $true)]
    [string] $ReleaseZip,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseChecksum,

    [Parameter(Mandatory = $true)]
    [string] $SbomPath,

    [Parameter(Mandatory = $true)]
    [string] $ProvenancePath,

    [Parameter(Mandatory = $true)]
    [string] $SigningReadinessPath,

    [Parameter(Mandatory = $true)]
    [string] $PublishedDirectory,

    [string] $Version = "0.1.0",

    [string] $RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$zip = Resolve-Path -LiteralPath $ReleaseZip
$checksum = Resolve-Path -LiteralPath $ReleaseChecksum
$sbomFile = Resolve-Path -LiteralPath $SbomPath
$provenanceFile = Resolve-Path -LiteralPath $ProvenancePath
$signingFile = Resolve-Path -LiteralPath $SigningReadinessPath
$published = Resolve-Path -LiteralPath $PublishedDirectory

function Get-Sha256Lower([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$checksumLine = (Get-Content -LiteralPath $checksum.Path -Raw).Trim()
if ($checksumLine -notmatch '^([0-9a-fA-F]{64})  (.+)$') {
    throw "Release checksum file has an invalid format."
}
$actualZipHash = Get-Sha256Lower $zip.Path
if ($Matches[1].ToLowerInvariant() -ne $actualZipHash) {
    throw "Release ZIP SHA-256 does not match its external checksum."
}
if ($Matches[2] -ne [IO.Path]::GetFileName($zip.Path)) {
    throw "Release checksum filename does not match the release ZIP."
}

$sbom = Get-Content -LiteralPath $sbomFile.Path -Raw | ConvertFrom-Json
if ($sbom.bomFormat -ne "CycloneDX") { throw "SBOM is not CycloneDX." }
if ($sbom.specVersion -ne "1.5") { throw "Unexpected CycloneDX version: $($sbom.specVersion)" }
if ([string]$sbom.metadata.component.name -ne "SkeletonKey Runner") { throw "SBOM root component is not SkeletonKey Runner." }
if ([string]$sbom.metadata.component.version -ne $Version) { throw "SBOM release version mismatch." }
$components = @($sbom.components)
if ($components.Count -eq 0) { throw "SBOM contains no dependency components." }
$refs = @{}
foreach ($component in $components) {
    $ref = [string]$component.'bom-ref'
    if ([string]::IsNullOrWhiteSpace($component.name) -or [string]::IsNullOrWhiteSpace($component.version) -or [string]::IsNullOrWhiteSpace($ref)) {
        throw "SBOM contains an incomplete component."
    }
    if ($refs.ContainsKey($ref)) { throw "SBOM contains duplicate bom-ref: $ref" }
    $refs[$ref] = $true
}

$runner = Join-Path $published "skeletonkey.exe"
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) { throw "Published skeletonkey.exe is missing." }
$signing = Get-Content -LiteralPath $signingFile.Path -Raw | ConvertFrom-Json
if ($signing.product -ne "SkeletonKey Runner") { throw "Signing readiness product mismatch." }
if ($signing.version -ne $Version) { throw "Signing readiness version mismatch." }
if ($signing.runtimeIdentifier -ne $RuntimeIdentifier) { throw "Signing readiness RID mismatch." }
if ($signing.state -notin @("ready-unsigned", "signed")) { throw "Unexpected signing readiness state: $($signing.state)" }
if ($signing.executableSha256 -ne (Get-Sha256Lower $runner)) { throw "Signing readiness executable hash does not match published skeletonkey.exe." }

$provenance = Get-Content -LiteralPath $provenanceFile.Path -Raw | ConvertFrom-Json
if ($provenance._type -ne "https://in-toto.io/Statement/v1") { throw "Unexpected provenance statement type." }
if ($provenance.predicateType -ne "https://slsa.dev/provenance/v1") { throw "Unexpected provenance predicate type." }
if ($provenance.predicate.buildDefinition.externalParameters.version -ne $Version) { throw "Provenance version mismatch." }
if ($provenance.predicate.buildDefinition.externalParameters.runtimeIdentifier -ne $RuntimeIdentifier) { throw "Provenance RID mismatch." }

$expectedSubjects = @{}
$expectedSubjects[[IO.Path]::GetFileName($zip.Path)] = $actualZipHash
$expectedSubjects[[IO.Path]::GetFileName($sbomFile.Path)] = (Get-Sha256Lower $sbomFile.Path)
$expectedSubjects[[IO.Path]::GetFileName($signingFile.Path)] = (Get-Sha256Lower $signingFile.Path)
foreach ($name in $expectedSubjects.Keys) {
    $subject = @($provenance.subject | Where-Object { $_.name -eq $name })
    if ($subject.Count -ne 1) { throw "Provenance must contain exactly one subject for $name." }
    if ([string]$subject[0].digest.sha256 -ne $expectedSubjects[$name]) {
        throw "Provenance subject SHA-256 mismatch for $name."
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot "sign-release.ps1") -PathType Leaf)) {
    throw "Production signing entry point is missing."
}

Write-Host "Supply-chain verification passed: $($components.Count) SBOM component(s), signing state '$($signing.state)', provenance subjects verified."
