param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PackageDirectory,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $ExpectedVersion = "0.1.0",

    [switch] $RequireSelfContained
)

$ErrorActionPreference = "Stop"
$package = Resolve-Path -LiteralPath $PackageDirectory
$manifestPath = Join-Path $package "manifest.json"
$checksumsPath = Join-Path $package "SHA256SUMS"

if (-not (Test-Path -LiteralPath $manifestPath)) { throw "manifest.json is missing from release package." }
if (-not (Test-Path -LiteralPath $checksumsPath)) { throw "SHA256SUMS is missing from release package." }

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.formatVersion -ne "1.0") { throw "Unsupported release manifest format: $($manifest.formatVersion)" }
if ($manifest.product -ne "SkeletonKey Runner") { throw "Unexpected release product: $($manifest.product)" }
if ($manifest.version -ne $ExpectedVersion) { throw "Release version mismatch: expected $ExpectedVersion, found $($manifest.version)." }
if ($manifest.runtimeIdentifier -ne $RuntimeIdentifier) { throw "Release RID mismatch: expected $RuntimeIdentifier, found $($manifest.runtimeIdentifier)." }
if ($RequireSelfContained -and -not [bool]$manifest.selfContained) { throw "Release package is not self-contained." }

$manifestEntries = @($manifest.files)
if ($manifestEntries.Count -eq 0) { throw "Release manifest contains no files." }

$checksumLines = @(Get-Content -LiteralPath $checksumsPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$checksums = @{}
foreach ($line in $checksumLines) {
    if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') { throw "Invalid SHA256SUMS line: $line" }
    $checksums[$Matches[2]] = $Matches[1].ToLowerInvariant()
}

$manifestPaths = @{}
foreach ($entry in $manifestEntries) {
    $relative = [string]$entry.path
    if ([string]::IsNullOrWhiteSpace($relative)) { throw "Release manifest contains an empty path." }
    if ($manifestPaths.ContainsKey($relative)) { throw "Duplicate release manifest path: $relative" }
    $manifestPaths[$relative] = $true

    $nativeRelative = $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $filePath = Join-Path $package $nativeRelative
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Manifest file is missing: $relative" }

    $file = Get-Item -LiteralPath $filePath
    if ([long]$entry.bytes -ne $file.Length) { throw "Size mismatch for $relative." }

    $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedHash = ([string]$entry.sha256).ToLowerInvariant()
    if ($actualHash -ne $expectedHash) { throw "Manifest SHA256 mismatch for $relative." }
    if (-not $checksums.ContainsKey($relative)) { throw "SHA256SUMS entry is missing for $relative." }
    if ($checksums[$relative] -ne $actualHash) { throw "SHA256SUMS mismatch for $relative." }
}

if ($checksums.Count -ne $manifestEntries.Count) {
    throw "SHA256SUMS entry count does not match manifest file count."
}

$payloadFiles = @(Get-ChildItem -LiteralPath $package -File -Recurse | Where-Object { $_.Name -notin @('manifest.json', 'SHA256SUMS') })
if ($payloadFiles.Count -ne $manifestEntries.Count) {
    throw "Release package contains files not represented by the manifest, or manifest entries are missing."
}

$runnerExe = Join-Path $package "skeletonkey.exe"
if ($RequireSelfContained -and -not (Test-Path -LiteralPath $runnerExe -PathType Leaf)) {
    throw "Self-contained release is missing skeletonkey.exe."
}

Write-Host "Release package integrity verification passed: $($manifestEntries.Count) payload file(s)."
