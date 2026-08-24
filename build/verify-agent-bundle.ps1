param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $BundleDirectory,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $ExpectedVersion = ""
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$bundle = Resolve-Path -LiteralPath $BundleDirectory
$manifestPath = Join-Path $bundle "agent-bundle.json"

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "agent-bundle.json is missing from the Agent bundle."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.formatVersion -ne "0.1") { throw "Unsupported Agent bundle format: $($manifest.formatVersion)" }
if ($manifest.product -ne "SkeletonKey Agent Runtime") { throw "Unexpected Agent bundle product: $($manifest.product)" }
if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion) -and $manifest.version -ne $ExpectedVersion) { throw "Agent bundle version mismatch: expected $ExpectedVersion, found $($manifest.version)." }
$actualVersion = [string] $manifest.version
if ([string]::IsNullOrWhiteSpace($actualVersion)) { throw "Agent bundle version is missing." }
if ($manifest.runtimeIdentifier -ne $RuntimeIdentifier) { throw "Agent bundle RID mismatch: expected $RuntimeIdentifier, found $($manifest.runtimeIdentifier)." }

$entries = @($manifest.files)
if ($entries.Count -eq 0) { throw "Agent bundle contains no file entries." }
$paths = @{}
foreach ($entry in $entries) {
    $relative = [string] $entry.path
    if ([string]::IsNullOrWhiteSpace($relative)) { throw "Agent bundle contains an empty path." }
    if ($relative -eq "agent-bundle.json") { throw "agent-bundle.json must not hash itself." }
    if ($paths.ContainsKey($relative)) { throw "Duplicate Agent bundle path: $relative" }
    $paths[$relative] = $true

    $nativeRelative = $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $filePath = Join-Path $bundle $nativeRelative
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Agent bundle file is missing: $relative" }
    $file = Get-Item -LiteralPath $filePath
    if ([long] $entry.bytes -ne $file.Length) { throw "Agent bundle size mismatch for $relative." }
    $actual = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne ([string] $entry.sha256).ToLowerInvariant()) { throw "Agent bundle SHA256 mismatch for $relative." }
}

$actualFiles = @(Get-ChildItem -LiteralPath $bundle -File -Recurse | Where-Object { $_.FullName -ne $manifestPath })
if ($actualFiles.Count -ne $entries.Count) {
    throw "Agent bundle contains an unmanifested file, or a manifest entry is missing."
}

$runtime = Join-Path $bundle "runtime"
& (Join-Path $PSScriptRoot "verify-release-package.ps1") $runtime -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $actualVersion -RequireSelfContained
if (-not $?) { throw "Embedded Runner package verification failed." }

$runner = Join-Path $runtime "skeletonkey.exe"
$runtimeHash = (Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash.ToLowerInvariant()
if ($runtimeHash -ne ([string] $manifest.runtimeExecutableSha256).ToLowerInvariant()) {
    throw "Agent bundle runtimeExecutableSha256 does not match skeletonkey.exe."
}

Write-Host "Agent bundle verification passed: $($entries.Count) manifested file(s), version $actualVersion, RID $RuntimeIdentifier."
