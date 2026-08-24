param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PublishedDirectory,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0",

    [string] $OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$published = Resolve-Path -LiteralPath $PublishedDirectory

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repo "artifacts\release"
}

if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]*$') { throw "Version contains unsupported release filename characters." }

& (Join-Path $PSScriptRoot "verify-release-package.ps1") $published -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version -RequireSelfContained
if ($LASTEXITCODE -ne 0) { throw "Published runner integrity verification failed." }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$name = "skeletonkey-$Version-$RuntimeIdentifier"
$zipPath = Join-Path $OutputDirectory "$name.zip"
$hashPath = Join-Path $OutputDirectory "$name.zip.sha256"

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
if (Test-Path -LiteralPath $hashPath) { Remove-Item -LiteralPath $hashPath -Force }

Compress-Archive -Path (Join-Path $published '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $hashPath -Encoding ascii

Write-Host "Release archive: $zipPath"
Write-Host "Release checksum: $hashPath"
