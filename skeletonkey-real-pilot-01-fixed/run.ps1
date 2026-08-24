$ErrorActionPreference = "Stop"

$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$runner = Join-Path $repo "src\SkeletonKey.Runner\SkeletonKey.Runner.csproj"
$workflow = Join-Path $PSScriptRoot "pilot-example-domain.workflow.json"
$locators = Join-Path $PSScriptRoot "locators"

Write-Host "1/4 VALIDATE"
dotnet run --project $runner -c Release -- validate --file $workflow
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "2/4 ANALYZE"
dotnet run --project $runner -c Release -- analyze --file $workflow --locator-directory $locators
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "3/4 PLAN"
dotnet run --project $runner -c Release -- plan --file $workflow --locator-directory $locators
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "4/4 REAL RUN"
dotnet run --project $runner -c Release -- run --file $workflow --locator-directory $locators --execution-id pilot-real-01-fixed --format ndjson --diagnostics
exit $LASTEXITCODE
