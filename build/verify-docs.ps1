param(
    [switch] $SkipToolRestore
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$docfxConfig = Join-Path $repo "docs\docfx.json"
$generatedApi = Join-Path $repo "docs\api"
$site = Join-Path $repo "artifacts\docs\site"

Push-Location $repo
try {
    if (-not $SkipToolRestore) {
        & dotnet tool restore
        if ($LASTEXITCODE -ne 0) { throw "DocFX tool restore failed." }
    }

    if (Test-Path -LiteralPath $generatedApi) {
        Remove-Item -LiteralPath $generatedApi -Recurse -Force
    }

    if (Test-Path -LiteralPath $site) {
        Remove-Item -LiteralPath $site -Recurse -Force
    }

    & dotnet docfx $docfxConfig
    if ($LASTEXITCODE -ne 0) { throw "DocFX documentation build failed." }

    $requiredFiles = @(
        (Join-Path $site "index.html"),
        (Join-Path $site "getting-started.html"),
        (Join-Path $site "configuration\environment-variables.html"),
        (Join-Path $site "architecture\0028-use-docfx-and-madr-for-project-documentation.html"),
        (Join-Path $generatedApi "toc.yml")
    )

    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Documentation build is missing required output: $requiredFile"
        }
    }

    $apiHtml = Get-ChildItem -LiteralPath (Join-Path $site "api") -Filter "*.html" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $apiHtml) {
        throw "DocFX API reference did not produce any HTML pages."
    }

    Write-Host "DocFX documentation site: $site"
    Write-Host "DocFX documentation verification passed."
}
finally {
    Pop-Location
}

$global:LASTEXITCODE = 0
