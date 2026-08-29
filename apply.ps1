param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [switch] $Force
)

$ErrorActionPreference = "Stop"
$expectedCommit = "31cc246e25ec5df9f7e17280707b30b9b7e9844e"
$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$overlay = Join-Path $PSScriptRoot "repository-overlay"

if (-not (Test-Path -LiteralPath (Join-Path $repository "SkeletonKey.sln"))) {
    throw "RepositoryRoot does not look like the SkeletonKey repository: $repository"
}

if (Get-Command git -ErrorAction SilentlyContinue) {
    Push-Location $repository
    try {
        $actualCommit = (& git rev-parse HEAD).Trim()
        if (-not $Force -and $actualCommit -ne $expectedCommit) {
            throw "Expected base commit $expectedCommit but repository HEAD is $actualCommit. Re-run with -Force only after reviewing the diff."
        }
    }
    finally {
        Pop-Location
    }
}

Get-ChildItem -LiteralPath $overlay -File -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($overlay.Length).TrimStart('\', '/')
    $destination = Join-Path $repository $relative
    $destinationDirectory = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}

$placeholder = Join-Path $repository "docs\architecture\0029-short-title.md"
if (Test-Path -LiteralPath $placeholder) {
    Remove-Item -LiteralPath $placeholder -Force
}

Write-Host "Standalone Export implementation applied to: $repository"
Write-Host "Next verification command:"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\build\verify-standalone-export.ps1"
