param(
    [ValidateRange(1, 65535)]
    [int] $Port = 8080,
    [switch] $SkipToolRestore
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$docfxConfig = Join-Path $repo "docs\docfx.json"

Push-Location $repo
try {
    if (-not $SkipToolRestore) {
        & dotnet tool restore
        if ($LASTEXITCODE -ne 0) { throw "DocFX tool restore failed." }
    }

    Write-Host "Building and serving SkeletonKey documentation on http://localhost:$Port"
    & dotnet docfx $docfxConfig --serve --hostname localhost --port $Port --open-browser
    if ($LASTEXITCODE -ne 0) { throw "DocFX local documentation server failed." }
}
finally {
    Pop-Location
}
