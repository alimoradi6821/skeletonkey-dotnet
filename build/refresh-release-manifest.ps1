param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PackageDirectory
)

$ErrorActionPreference = "Stop"
$package = Resolve-Path -LiteralPath $PackageDirectory
$manifestPath = Join-Path $package "manifest.json"
$checksumsPath = Join-Path $package "SHA256SUMS"

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "manifest.json is missing from published runner directory."
}

$existing = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$payload = @(Get-ChildItem -LiteralPath $package -File -Recurse |
    Where-Object { $_.Name -notin @("manifest.json", "SHA256SUMS") } |
    Sort-Object FullName |
    ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($package.Path.Length).TrimStart('\').Replace('\', '/')
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })

if ($payload.Count -eq 0) { throw "Published runner contains no payload files." }

$manifest = [ordered]@{
    formatVersion = [string]$existing.formatVersion
    product = [string]$existing.product
    version = [string]$existing.version
    targetFramework = [string]$existing.targetFramework
    runtimeIdentifier = [string]$existing.runtimeIdentifier
    selfContained = [bool]$existing.selfContained
    files = $payload
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8
$payload | ForEach-Object { "$($_.sha256)  $($_.path)" } | Set-Content -LiteralPath $checksumsPath -Encoding ascii

Write-Host "Release manifest refreshed: $($payload.Count) payload file(s)."
