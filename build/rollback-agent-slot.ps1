param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $InstallRoot,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $ExpectedVersion = ""
)

$ErrorActionPreference = "Stop"

if (-not ("SkeletonKeyRollbackAtomicFile" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class SkeletonKeyRollbackAtomicFile
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
$statePath = Join-Path $install "deployment-state.json"
if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) { throw "Agent deployment state does not exist." }
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
$current = [string] $state.activeSlot
$previous = [string] $state.previousSlot
if ([string]::IsNullOrWhiteSpace($previous)) { throw "No previous Agent slot is available for rollback." }
if ($previous -notin @("blue", "green")) { throw "Invalid previous Agent slot '$previous'." }

$previousPath = Join-Path $install "slots\$previous"
& (Join-Path $PSScriptRoot "verify-agent-bundle.ps1") $previousPath -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $ExpectedVersion
if (-not $?) { throw "Rollback target Agent slot verification failed." }

$previousManifest = Get-Content -LiteralPath (Join-Path $previousPath "agent-bundle.json") -Raw | ConvertFrom-Json
$previousVersion = [string] $previousManifest.version
$previousManifestHash = (Get-FileHash -LiteralPath (Join-Path $previousPath "agent-bundle.json") -Algorithm SHA256).Hash.ToLowerInvariant()
$newState = [ordered]@{
    formatVersion = "0.1"
    revision = [int64] $state.revision + 1
    activeSlot = $previous
    previousSlot = $current
    activeVersion = $previousVersion
    previousVersion = [string] $state.activeVersion
    activeBundleManifestSha256 = $previousManifestHash
    previousBundleManifestSha256 = [string] $state.activeBundleManifestSha256
    runtimeIdentifier = $RuntimeIdentifier
    updatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
}
$temp = "$statePath.$PID.tmp"
$newState | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temp -Encoding utf8
try {
    [SkeletonKeyRollbackAtomicFile]::Replace($temp, $statePath)
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force }
}
Write-Host "Agent rollback completed: active=$previous previous=$current revision=$($newState.revision)"
