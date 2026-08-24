param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0",

    [switch] $RequireSignedRelease,

    [string] $OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
if ($Version -ne "0.1.0") { throw "Phase 30 GA is frozen to version 0.1.0; found '$Version'." }
if ($Version -match '-') { throw "GA version must not contain a prerelease suffix." }

$published = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"
& (Join-Path $PSScriptRoot "verify-release-package.ps1") $published -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version -RequireSelfContained
if (-not $?) { throw "GA published Runner integrity verification failed." }

$runner = Join-Path $published "skeletonkey.exe"
$versionOutput = @(& $runner version 2>&1 | ForEach-Object { [string] $_ })
if ($LASTEXITCODE -ne 0) { throw "GA published Runner version command failed." }
$versionEnvelope = (($versionOutput -join [Environment]::NewLine).Trim()) | ConvertFrom-Json
$informationalVersion = [string] $versionEnvelope.result.informationalVersion
$versionMatches = $informationalVersion -eq $Version -or $informationalVersion.StartsWith("$Version+", [StringComparison]::Ordinal)
if (-not $versionMatches) { throw "Published Runner is not GA $Version. Found informational version '$informationalVersion'." }

$releaseBase = Join-Path $repo "artifacts\release\skeletonkey-$Version-$RuntimeIdentifier"
$releaseZip = "$releaseBase.zip"
$releaseChecksum = "$releaseBase.zip.sha256"
$sbomPath = "$releaseBase.sbom.cdx.json"
$provenancePath = "$releaseBase.provenance.json"
$signingPath = "$releaseBase.signing-readiness.json"
foreach ($path in @($releaseZip, $releaseChecksum, $sbomPath, $provenancePath, $signingPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required GA release artifact is missing: $path" }
}

& (Join-Path $PSScriptRoot "verify-supply-chain.ps1") `
    -ReleaseZip $releaseZip `
    -ReleaseChecksum $releaseChecksum `
    -SbomPath $sbomPath `
    -ProvenancePath $provenancePath `
    -SigningReadinessPath $signingPath `
    -PublishedDirectory $published `
    -Version $Version `
    -RuntimeIdentifier $RuntimeIdentifier
if (-not $?) { throw "GA supply-chain verification failed." }

$signing = Get-Content -LiteralPath $signingPath -Raw | ConvertFrom-Json
if ($RequireSignedRelease -and $signing.state -ne "signed") {
    throw "GA public-distribution gate requires a valid Authenticode signature; current state is '$($signing.state)'."
}

$agentArchive = Join-Path $repo "artifacts\agent\skeletonkey-agent-runtime-$Version-$RuntimeIdentifier.zip"
$agentChecksum = "$agentArchive.sha256"
if (-not (Test-Path -LiteralPath $agentArchive -PathType Leaf)) { throw "GA Agent bundle is missing." }
if (-not (Test-Path -LiteralPath $agentChecksum -PathType Leaf)) { throw "GA Agent bundle checksum is missing." }
$agentChecksumLine = (Get-Content -LiteralPath $agentChecksum -Raw).Trim()
if ($agentChecksumLine -notmatch '^([0-9a-fA-F]{64})\s{2}(.+)$') { throw "GA Agent bundle checksum file is invalid." }
$agentHash = (Get-FileHash -LiteralPath $agentArchive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($Matches[1].ToLowerInvariant() -ne $agentHash) { throw "GA Agent bundle checksum mismatch." }
if ($Matches[2] -ne [IO.Path]::GetFileName($agentArchive)) { throw "GA Agent bundle checksum filename mismatch." }

$verificationRoot = Join-Path $repo "artifacts\ga\agent-bundle-verification"
if (Test-Path -LiteralPath $verificationRoot) { Remove-Item -LiteralPath $verificationRoot -Recurse -Force }
New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
try {
    Expand-Archive -LiteralPath $agentArchive -DestinationPath $verificationRoot -Force
    & (Join-Path $PSScriptRoot "verify-agent-bundle.ps1") $verificationRoot -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version
    if (-not $?) { throw "GA Agent bundle verification failed." }
}
finally {
    if (Test-Path -LiteralPath $verificationRoot) { Remove-Item -LiteralPath $verificationRoot -Recurse -Force }
}

$canaryPath = Join-Path $repo "artifacts\canary\phase-029-canary-report.json"
if (-not (Test-Path -LiteralPath $canaryPath -PathType Leaf)) { throw "Phase 29 canary report is missing." }
$canary = Get-Content -LiteralPath $canaryPath -Raw | ConvertFrom-Json
if ($canary.status -ne "passed" -or $canary.phase -ne 29 -or $canary.version -ne $Version) {
    throw "Phase 29 canary report does not certify GA version $Version."
}

$soakCandidates = @(
    (Join-Path $repo "artifacts\soak\phase-028-soak-report.json"),
    (Join-Path $repo "artifacts\soak\phase-028-clean-machine-soak-report.json")
)
$soakPath = $soakCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace([string] $soakPath)) { throw "Phase 28 soak report is missing." }
$soak = Get-Content -LiteralPath $soakPath -Raw | ConvertFrom-Json
if ($soak.status -ne "passed" -or $soak.phase -ne 28) { throw "Phase 28 soak report is not passing." }

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = "$releaseBase.ga.json"
}

$report = [ordered]@{
    formatVersion = "1.0"
    product = "SkeletonKey Runner"
    release = "GA"
    version = $Version
    runtimeIdentifier = $RuntimeIdentifier
    informationalVersion = $informationalVersion
    signingState = [string] $signing.state
    signedReleaseRequired = [bool] $RequireSignedRelease
    compatibility = [ordered]@{
        workflowSpecification = "0.1.0"
        workflowSchema = "https://schemas.skeletonkey.dev/workflow/0.1/schema.json"
        locatorSpecification = "0.1.0"
        locatorSchema = "https://schemas.skeletonkey.dev/locators/0.1/schema.json"
        checkpointCurrentFormat = "0.3"
        checkpointAcceptedLegacyFormats = @("0.2", "0.1")
        localPluginManifestSchema = "0.1"
        operatingSystem = "Windows x64"
        targetFramework = "net10.0-windows"
    }
    artifacts = [ordered]@{
        releaseZip = [IO.Path]::GetFileName($releaseZip)
        releaseZipSha256 = (Get-FileHash -LiteralPath $releaseZip -Algorithm SHA256).Hash.ToLowerInvariant()
        sbomSha256 = (Get-FileHash -LiteralPath $sbomPath -Algorithm SHA256).Hash.ToLowerInvariant()
        provenanceSha256 = (Get-FileHash -LiteralPath $provenancePath -Algorithm SHA256).Hash.ToLowerInvariant()
        signingReadinessSha256 = (Get-FileHash -LiteralPath $signingPath -Algorithm SHA256).Hash.ToLowerInvariant()
        agentBundle = [IO.Path]::GetFileName($agentArchive)
        agentBundleSha256 = $agentHash
    }
    acceptance = @(
        "phase-0-29-regression",
        "security-audit",
        "release-integrity",
        "supply-chain",
        "phase-28-soak",
        "phase-29-blue-green-canary",
        "phase-29-crash-recovery",
        "phase-30-storage-failure",
        "compatibility-freeze"
    )
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
}
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding utf8

Write-Host "GA release report: $OutputPath"
Write-Host "SkeletonKey $Version GA finalization passed with signing state '$($signing.state)'."
