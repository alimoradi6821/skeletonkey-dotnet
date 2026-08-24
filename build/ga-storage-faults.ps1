param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PublishedDirectory,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$published = Resolve-Path -LiteralPath $PublishedDirectory
$runner = Join-Path $published "skeletonkey.exe"
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw "Phase 30 storage fault gate requires a self-contained skeletonkey.exe."
}

# Provider-level regression coverage: checkpoint root failure must be SKR3005 and artifact persistence failure must be SKR2029.
dotnet test tests/SkeletonKey.Artifacts.FileSystem.Tests/SkeletonKey.Artifacts.FileSystem.Tests.csproj `
    --configuration Release `
    --no-build `
    --filter "Category=Phase30GA"
if ($LASTEXITCODE -ne 0) { throw "Phase 30 filesystem storage-failure regression tests failed." }

# Published-binary fault: the checkpoint path exists as a file, so the store cannot use it as a directory.
$faultRoot = Join-Path $repo "artifacts\fault-injection\phase-030"
if (Test-Path -LiteralPath $faultRoot) { Remove-Item -LiteralPath $faultRoot -Recurse -Force }
New-Item -ItemType Directory -Path $faultRoot -Force | Out-Null
$blockedCheckpointRoot = Join-Path $faultRoot "checkpoint-root"
"blocked" | Set-Content -LiteralPath $blockedCheckpointRoot -Encoding ascii
$workflow = Join-Path $repo "tests\fixtures\validation\valid-minimal.workflow.json"
$output = @(& $runner run --file $workflow --execution-id phase-030-storage-failure --checkpoint-directory $blockedCheckpointRoot --format json 2>&1 | ForEach-Object { [string] $_ })
$exitCode = $LASTEXITCODE
$raw = ($output -join [Environment]::NewLine).Trim()
if ($exitCode -eq 0) { throw "Unavailable checkpoint root unexpectedly succeeded." }
if ([string]::IsNullOrWhiteSpace($raw)) { throw "Unavailable checkpoint root returned no machine-readable envelope." }
$envelope = $raw | ConvertFrom-Json
$codes = @($envelope.issues | ForEach-Object { [string] $_.code })
if ($codes -notcontains "SKR3005") {
    throw "Unavailable checkpoint root returned unexpected code(s): $($codes -join ', '); expected SKR3005."
}

Write-Host "Phase 30 storage fault passed: unavailable checkpoint root rejected with SKR3005."
Write-Host "Phase 30 filesystem persistence failure gate passed."
$global:LASTEXITCODE = 0
