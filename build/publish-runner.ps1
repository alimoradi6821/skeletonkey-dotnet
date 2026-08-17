param(
    [Parameter(Position = 0)]
    [string] $RuntimeIdentifier = "win-x64",

    [Parameter(Position = 1)]
    [string] $OutputDirectory = "",

    [switch] $SelfContained
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\SkeletonKey.Runner\SkeletonKey.Runner.csproj"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot "..\artifacts\runner\$RuntimeIdentifier"
}

$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$repoPath = [IO.Path]::GetFullPath($repo.Path).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $OutputDirectory.StartsWith($repoPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be inside the repository."
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$publishSingleFileValue = if ($SelfContained) { "true" } else { "false" }
$useAppHostValue = if ($SelfContained) { "true" } else { "false" }
dotnet publish $project --configuration Release --runtime $RuntimeIdentifier --self-contained $selfContainedValue --output $OutputDirectory /p:PublishTrimmed=false /p:PublishSingleFile=$publishSingleFileValue /p:UseAppHost=$useAppHostValue
if ($LASTEXITCODE -ne 0) { throw "Runner publish failed with exit code $LASTEXITCODE." }

$manifestFiles = Get-ChildItem -LiteralPath $OutputDirectory -File -Recurse |
    Sort-Object FullName |
    ForEach-Object {
        [pscustomobject]@{
            path = $_.FullName.Substring($OutputDirectory.Length).TrimStart('\').Replace('\', '/')
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

$manifest = [pscustomobject]@{
    formatVersion = "1.0"
    product = "SkeletonKey Runner"
    targetFramework = "net10.0"
    runtimeIdentifier = $RuntimeIdentifier
    selfContained = [bool]$SelfContained
    files = @($manifestFiles)
}

$manifestPath = Join-Path $OutputDirectory "manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
$checksumsPath = Join-Path $OutputDirectory "SHA256SUMS"
$manifestFiles | ForEach-Object { "$($_.sha256)  $($_.path)" } | Set-Content -LiteralPath $checksumsPath -Encoding ascii

Write-Output $manifestPath
Write-Output $checksumsPath
