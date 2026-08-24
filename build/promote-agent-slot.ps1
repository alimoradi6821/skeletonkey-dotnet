param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $InstallRoot,

    [Parameter(Mandatory = $true)]
    [ValidateSet("blue", "green")]
    [string] $Slot,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $ExpectedVersion = ""
)

$ErrorActionPreference = "Stop"

if (-not ("SkeletonKeyPromoteAtomicFile" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class SkeletonKeyPromoteAtomicFile
{
    private const int MoveFileReplaceExisting = 0x1;
    private const int MoveFileWriteThrough = 0x8;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

    public static void Replace(string sourcePath, string destinationPath)
    {
        if (!MoveFileEx(sourcePath, destinationPath, MoveFileReplaceExisting | MoveFileWriteThrough))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Atomic deployment-state replacement failed.");
        }
    }
}
"@
}
$install = [IO.Path]::GetFullPath($InstallRoot)
$slotPath = Join-Path $install "slots\$Slot"
& (Join-Path $PSScriptRoot "verify-agent-bundle.ps1") $slotPath -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $ExpectedVersion
if (-not $?) { throw "Candidate Agent slot verification failed." }

$statePath = Join-Path $install "deployment-state.json"
$old = $null
if (Test-Path -LiteralPath $statePath -PathType Leaf) {
    $old = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
}
$oldActive = if ($null -eq $old) { $null } else { [string] $old.activeSlot }
$oldPrevious = if ($null -eq $old) { $null } else { [string] $old.previousSlot }
$oldActiveVersion = if ($null -eq $old) { $null } else { [string] $old.activeVersion }
$oldActiveManifestHash = if ($null -eq $old) { $null } else { [string] $old.activeBundleManifestSha256 }
$previous = if (-not [string]::IsNullOrWhiteSpace($oldActive) -and $oldActive -ne $Slot) { $oldActive } else { $oldPrevious }
$revision = if ($null -eq $old) { 1 } else { [int64] $old.revision + 1 }
$slotManifest = Get-Content -LiteralPath (Join-Path $slotPath "agent-bundle.json") -Raw | ConvertFrom-Json
$slotVersion = [string] $slotManifest.version
$slotManifestHash = (Get-FileHash -LiteralPath (Join-Path $slotPath "agent-bundle.json") -Algorithm SHA256).Hash.ToLowerInvariant()

$newState = [ordered]@{
    formatVersion = "0.1"
    revision = $revision
    activeSlot = $Slot
    previousSlot = $previous
    activeVersion = $slotVersion
    previousVersion = if ($previous -eq $oldActive) { $oldActiveVersion } elseif ($null -eq $old) { $null } else { [string] $old.previousVersion }
    activeBundleManifestSha256 = $slotManifestHash
    previousBundleManifestSha256 = if ($previous -eq $oldActive) { $oldActiveManifestHash } elseif ($null -eq $old) { $null } else { [string] $old.previousBundleManifestSha256 }
    runtimeIdentifier = $RuntimeIdentifier
    updatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
}
New-Item -ItemType Directory -Path $install -Force | Out-Null
$temp = "$statePath.$PID.tmp"
$newState | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temp -Encoding utf8
try {
    [SkeletonKeyPromoteAtomicFile]::Replace($temp, $statePath)
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force }
}
Write-Host "Agent slot promoted: active=$Slot version=$slotVersion previous=$previous revision=$revision"
