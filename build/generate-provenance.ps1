param(
    [Parameter(Mandatory = $true)]
    [string] $ReleaseZip,

    [Parameter(Mandatory = $true)]
    [string] $SbomPath,

    [Parameter(Mandatory = $true)]
    [string] $SigningReadinessPath,

    [string] $Version = "0.1.0",

    [string] $RuntimeIdentifier = "win-x64",

    [string] $OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$zip = Resolve-Path -LiteralPath $ReleaseZip
$sbom = Resolve-Path -LiteralPath $SbomPath
$signing = Resolve-Path -LiteralPath $SigningReadinessPath

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repo "artifacts\release\skeletonkey-$Version-$RuntimeIdentifier.provenance.json"
}

function Get-Sha256Lower([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$excludedPattern = '(^|/)(bin|obj|artifacts|\.git|\.vs|TestResults|coverage)(/|$)'
$sourceLines = @()
foreach ($file in @(Get-ChildItem -LiteralPath $repo -File -Recurse | Sort-Object FullName)) {
    $relative = $file.FullName.Substring($repo.Path.Length).TrimStart('\').Replace('\', '/')
    if ($relative -match $excludedPattern) { continue }
    $sourceLines += "$relative`t$(Get-Sha256Lower $file.FullName)"
}
if ($sourceLines.Count -eq 0) { throw "Source tree digest contains no files." }
$treeBytes = [Text.Encoding]::UTF8.GetBytes(($sourceLines -join "`n"))
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $treeHashBytes = $sha256.ComputeHash($treeBytes)
}
finally {
    $sha256.Dispose()
}
$treeHash = ([BitConverter]::ToString($treeHashBytes)).Replace("-", "").ToLowerInvariant()

$gitCommit = $null
$gitDirty = $null
$git = Get-Command git -ErrorAction SilentlyContinue
if ($null -ne $git -and (Test-Path -LiteralPath (Join-Path $repo ".git"))) {
    $commitOutput = (& git -C $repo rev-parse HEAD 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -eq 0 -and $commitOutput -match '^[0-9a-fA-F]{40,64}$') {
        $gitCommit = $commitOutput.ToLowerInvariant()
        $dirtyOutput = (& git -C $repo status --porcelain 2>$null | Out-String)
        $gitDirty = -not [string]::IsNullOrWhiteSpace($dirtyOutput)
    }
}

$dotnetVersion = (& dotnet --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnetVersion)) {
    throw "Unable to determine the .NET SDK version for provenance."
}

$builderId = "local://windows-powershell"
$invocationId = "local-$([Guid]::NewGuid())"
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_RUN_ID)) {
    $server = if ([string]::IsNullOrWhiteSpace($env:GITHUB_SERVER_URL)) { "https://github.com" } else { $env:GITHUB_SERVER_URL.TrimEnd('/') }
    $repository = if ([string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) { "unknown/unknown" } else { $env:GITHUB_REPOSITORY }
    $builderId = "$server/$repository/actions/runs/$($env:GITHUB_RUN_ID)"
    $invocationId = "github-$($env:GITHUB_RUN_ID)-$($env:GITHUB_RUN_ATTEMPT)"
}

$materials = @(
    [ordered]@{ uri = "file:source-tree"; digest = [ordered]@{ sha256 = $treeHash } },
    [ordered]@{ uri = "file:Directory.Build.props"; digest = [ordered]@{ sha256 = (Get-Sha256Lower (Join-Path $repo "Directory.Build.props")) } },
    [ordered]@{ uri = "file:Directory.Packages.props"; digest = [ordered]@{ sha256 = (Get-Sha256Lower (Join-Path $repo "Directory.Packages.props")) } },
    [ordered]@{ uri = "file:global.json"; digest = [ordered]@{ sha256 = (Get-Sha256Lower (Join-Path $repo "global.json")) } },
    [ordered]@{ uri = "file:SkeletonKey.sln"; digest = [ordered]@{ sha256 = (Get-Sha256Lower (Join-Path $repo "SkeletonKey.sln")) } }
)
if ($null -ne $gitCommit) {
    $gitDigest = [ordered]@{}
    if ($gitCommit.Length -eq 64) {
        $gitDigest["sha256"] = $gitCommit
    }
    else {
        $gitDigest["sha1"] = $gitCommit
    }
    $materials += [ordered]@{ uri = "git+local://repository@$gitCommit"; digest = $gitDigest }
}

$statement = [ordered]@{
    _type = "https://in-toto.io/Statement/v1"
    subject = @(
        [ordered]@{ name = [IO.Path]::GetFileName($zip.Path); digest = [ordered]@{ sha256 = (Get-Sha256Lower $zip.Path) } },
        [ordered]@{ name = [IO.Path]::GetFileName($sbom.Path); digest = [ordered]@{ sha256 = (Get-Sha256Lower $sbom.Path) } },
        [ordered]@{ name = [IO.Path]::GetFileName($signing.Path); digest = [ordered]@{ sha256 = (Get-Sha256Lower $signing.Path) } }
    )
    predicateType = "https://slsa.dev/provenance/v1"
    predicate = [ordered]@{
        buildDefinition = [ordered]@{
            buildType = "https://skeletonkey.dev/buildtypes/windows-dotnet-runner/v1"
            externalParameters = [ordered]@{
                version = $Version
                runtimeIdentifier = $RuntimeIdentifier
                configuration = "Release"
                selfContained = $true
            }
            internalParameters = [ordered]@{
                dotnetSdk = $dotnetVersion
                os = [Environment]::OSVersion.VersionString
                powershell = $PSVersionTable.PSVersion.ToString()
                sourceFileCount = $sourceLines.Count
                gitCommit = $gitCommit
                gitDirty = $gitDirty
            }
            resolvedDependencies = $materials
        }
        runDetails = [ordered]@{
            builder = [ordered]@{ id = $builderId }
            metadata = [ordered]@{
                invocationId = $invocationId
                generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
            }
        }
    }
}

$parent = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$statement | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $OutputPath -Encoding utf8

Write-Host "SLSA-compatible provenance statement: $OutputPath"
Write-Host "Source tree SHA256: $treeHash"
