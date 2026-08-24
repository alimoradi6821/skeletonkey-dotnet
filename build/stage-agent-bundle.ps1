param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $BundleArchive,

    [Parameter(Mandatory = $true, Position = 1)]
    [string] $InstallRoot,

    [Parameter(Mandatory = $true)]
    [ValidateSet("blue", "green")]
    [string] $Slot,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $ExpectedVersion = "0.1.0"
)

$ErrorActionPreference = "Stop"
$archive = (Resolve-Path -LiteralPath $BundleArchive).Path
$install = [IO.Path]::GetFullPath($InstallRoot)
New-Item -ItemType Directory -Path $install -Force | Out-Null

$checksumPath = "$archive.sha256"
if (Test-Path -LiteralPath $checksumPath -PathType Leaf) {
    $line = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
    if ($line -notmatch '^([0-9a-fA-F]{64})\s{2}.+$') { throw "Invalid adjacent Agent bundle checksum file." }
    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Matches[1].ToLowerInvariant()) { throw "Agent bundle archive checksum mismatch." }
}

$statePath = Join-Path $install "deployment-state.json"
if (Test-Path -LiteralPath $statePath -PathType Leaf) {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ([string] $state.activeSlot -eq $Slot) {
        throw "Refusing to overwrite active Agent slot '$Slot'. Stage the inactive slot instead."
    }
}

$stagingRoot = Join-Path $install "staging"
$slotsRoot = Join-Path $install "slots"
$staging = Join-Path $stagingRoot ([Guid]::NewGuid().ToString("N"))
$slotPath = Join-Path $slotsRoot $Slot
New-Item -ItemType Directory -Path $stagingRoot, $slotsRoot -Force | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $stagingFull = [IO.Path]::GetFullPath($staging).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($entry in $zip.Entries) {
        if ([string]::IsNullOrWhiteSpace($entry.FullName)) { continue }
        $destination = [IO.Path]::GetFullPath((Join-Path $staging $entry.FullName))
        if (-not $destination.StartsWith($stagingFull, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Agent bundle archive contains an entry outside the staging root: $($entry.FullName)"
        }
    }
}
finally {
    $zip.Dispose()
}
Expand-Archive -LiteralPath $archive -DestinationPath $staging -Force

& (Join-Path $PSScriptRoot "verify-agent-bundle.ps1") $staging -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $ExpectedVersion
if (-not $?) { throw "Staged Agent bundle verification failed." }

if (Test-Path -LiteralPath $slotPath) { Remove-Item -LiteralPath $slotPath -Recurse -Force }
Move-Item -LiteralPath $staging -Destination $slotPath

New-Item -ItemType Directory -Path (Join-Path $install "state\checkpoints"), (Join-Path $install "state\artifacts"), (Join-Path $install "state\logs") -Force | Out-Null
Write-Host "Agent slot staged: $Slot -> $slotPath"
