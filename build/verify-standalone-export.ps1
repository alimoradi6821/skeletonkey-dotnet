param(
    [string] $RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")

Write-Host "[standalone] running Runner.Core tests"
dotnet test (Join-Path $repo "tests\SkeletonKey.Runner.Core.Tests\SkeletonKey.Runner.Core.Tests.csproj") --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Runner Core standalone tests failed." }

Write-Host "[standalone] running conformance schema tests"
dotnet test (Join-Path $repo "tests\SkeletonKey.Conformance.Tests\SkeletonKey.Conformance.Tests.csproj") --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Standalone schema conformance tests failed." }

Write-Host "[standalone] building runner"
dotnet build (Join-Path $repo "src\SkeletonKey.Runner\SkeletonKey.Runner.csproj") --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Runner build failed." }

$work = Join-Path $repo "artifacts\standalone-export-smoke"
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Path $work | Out-Null

$onceSettings = Join-Path $work "once.execution.settings.json"
@'
{
  "specVersion": "0.1",
  "schedule": { "type": "once" }
}
'@ | Set-Content -LiteralPath $onceSettings -Encoding utf8

$intervalSettings = Join-Path $work "interval.execution.settings.json"
@'
{
  "specVersion": "0.1",
  "schedule": { "type": "interval", "interval": "PT1S" },
  "execution": {
    "runImmediately": true,
    "overlap": "skip",
    "continueAfterFailure": true
  }
}
'@ | Set-Content -LiteralPath $intervalSettings -Encoding utf8

$workflow = Join-Path $repo "examples\minimal.workflow.json"
$onceOutput = Join-Path $work "minimal-once.exe"
$intervalOutput = Join-Path $work "minimal-interval.exe"
$runner = Join-Path $repo "src\SkeletonKey.Runner\bin\Release\net10.0-windows\skeletonkey.dll"

Write-Host "[standalone] exporting once application"
$onceEnvelopeText = (& dotnet $runner export standalone --workflow $workflow --settings $onceSettings --runtime $RuntimeIdentifier --output $onceOutput) -join "`n"
if ($LASTEXITCODE -ne 0) { throw "Standalone once export command failed: $onceEnvelopeText" }
$onceEnvelope = $onceEnvelopeText | ConvertFrom-Json
if (-not $onceEnvelope.accepted) { throw "Standalone once export envelope was not accepted." }
if (-not (Test-Path -LiteralPath $onceOutput)) { throw "Standalone once executable was not produced." }

Write-Host "[standalone] exporting interval application"
$intervalEnvelopeText = (& dotnet $runner export standalone --workflow $workflow --settings $intervalSettings --runtime $RuntimeIdentifier --output $intervalOutput) -join "`n"
if ($LASTEXITCODE -ne 0) { throw "Standalone interval export command failed: $intervalEnvelopeText" }
$intervalEnvelope = $intervalEnvelopeText | ConvertFrom-Json
if (-not $intervalEnvelope.accepted) { throw "Standalone interval export envelope was not accepted." }
if (-not (Test-Path -LiteralPath $intervalOutput)) { throw "Standalone interval executable was not produced." }
if ($onceEnvelope.result.packageId -eq $intervalEnvelope.result.packageId) {
    throw "Changing execution settings did not change the standalone package identity."
}

Write-Host "[standalone] running sealed once application"
& $onceOutput
if ($LASTEXITCODE -ne 0) { throw "Generated standalone executable failed its once smoke run with exit code $LASTEXITCODE." }

Write-Host "[standalone] proving runtime workflow replacement is rejected"
& $onceOutput --workflow $workflow
if ($LASTEXITCODE -ne 2) { throw "Generated standalone executable did not reject runtime arguments with usage exit code 2." }

Write-Host "[standalone] PASS"
Write-Host "  once:     $onceOutput"
Write-Host "  interval: $intervalOutput"
