param(
    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0",

    [ValidateRange(1, 10000)]
    [int] $CoreIterations = 200,

    [ValidateRange(1, 1000)]
    [int] $BrowserIterations = 30,

    [ValidateRange(0, 180)]
    [int] $MinimumSoakMinutes = 5
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        throw "Phase 28 verification must run on Windows."
    }

    & (Join-Path $PSScriptRoot "verify-phase-027.ps1") -RuntimeIdentifier $RuntimeIdentifier -Version $Version
    if (-not $?) { throw "Phase 27 supply-chain regression gate failed." }

    dotnet test tests/SkeletonKey.Runner.Core.Tests/SkeletonKey.Runner.Core.Tests.csproj `
        --configuration Release `
        --no-build `
        --filter "Category=Phase28Soak"
    if ($LASTEXITCODE -ne 0) { throw "Phase 28 in-process Runner soak failed." }

    $published = Join-Path $repo "artifacts\runner\$RuntimeIdentifier-self-contained"
    $report = Join-Path $repo "artifacts\soak\phase-028-soak-report.json"
    & (Join-Path $PSScriptRoot "soak-runner.ps1") $published `
        -RuntimeIdentifier $RuntimeIdentifier `
        -CoreIterations $CoreIterations `
        -BrowserIterations $BrowserIterations `
        -MinimumSoakMinutes $MinimumSoakMinutes `
        -OutputPath $report
    if (-not $?) { throw "Phase 28 published-binary resource soak failed." }

    if (-not (Test-Path -LiteralPath $report -PathType Leaf)) {
        throw "Phase 28 soak report was not generated."
    }

    $reportJson = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($reportJson.status -ne "passed" -or $reportJson.phase -ne 28) {
        throw "Phase 28 soak report did not record a passing Phase 28 result."
    }

    Write-Host "Phase 0-28 stress, soak, and resource-leak verification passed."
}
finally {
    Pop-Location
}
