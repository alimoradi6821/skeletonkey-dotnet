param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PublishedDirectory,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $ExpectedVersion = "0.1.0"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$published = Resolve-Path -LiteralPath $PublishedDirectory
$runner = Join-Path $published "skeletonkey.exe"
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw "Phase 26 fault injection requires a self-contained skeletonkey.exe."
}

$faultRoot = Join-Path $repo "artifacts\fault-injection\phase-026"
if (Test-Path -LiteralPath $faultRoot) { Remove-Item -LiteralPath $faultRoot -Recurse -Force }
New-Item -ItemType Directory -Path $faultRoot -Force | Out-Null

function Assert-FailedEnvelope {
    param(
        [Parameter(Mandatory = $true)] [object[]] $Output,
        [Parameter(Mandatory = $true)] [int] $ExitCode,
        [Parameter(Mandatory = $true)] [string] $ExpectedCode,
        [Parameter(Mandatory = $true)] [string] $Scenario
    )

    if ($ExitCode -eq 0) { throw "$Scenario unexpectedly succeeded." }
    $json = ($Output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($json)) { throw "$Scenario returned no machine-readable envelope." }
    $envelope = $json | ConvertFrom-Json
    if ([bool]$envelope.accepted) { throw "$Scenario returned accepted=true after an injected failure." }
    $codes = @($envelope.issues | ForEach-Object { [string]$_.code })
    if ($codes -notcontains $ExpectedCode) {
        throw "$Scenario returned unexpected code(s): $($codes -join ', '); expected $ExpectedCode."
    }
}

# Fault 1: integrity-protected checkpoint payload is tampered after a successful run.
$checkpointRoot = Join-Path $faultRoot "checkpoint"
$workflow = Join-Path $repo "tests\fixtures\conformance\valid\core-return.workflow.json"
& $runner run --file $workflow --execution-id phase-026-corrupt-checkpoint --checkpoint-directory $checkpointRoot | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Could not create the baseline checkpoint used by Phase 26 fault injection." }
$checkpoint = @(Get-ChildItem -LiteralPath $checkpointRoot -Filter 'checkpoint-*.json' -File)
if ($checkpoint.Count -ne 1) { throw "Expected exactly one checkpoint payload for the corruption test." }
$envelope = Get-Content -LiteralPath $checkpoint[0].FullName -Raw | ConvertFrom-Json
$envelope.sha256 = ('0' * 64)
$envelope | ConvertTo-Json -Compress | Set-Content -LiteralPath $checkpoint[0].FullName -Encoding utf8
$checkpointOutput = @(& $runner resume --file $workflow --execution-id phase-026-corrupt-checkpoint --checkpoint-directory $checkpointRoot)
$checkpointExit = $LASTEXITCODE
Assert-FailedEnvelope -Output $checkpointOutput -ExitCode $checkpointExit -ExpectedCode "SKR3003" -Scenario "Tampered checkpoint"
Write-Host "Fault injection passed: tampered checkpoint rejected with SKR3003."

# Fault 2: explicit plugin assembly hash does not match the manifest.
$pluginRoot = Join-Path $faultRoot "plugin"
New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
$pluginSource = Join-Path $repo "tests\SkeletonKey.Runner.Core.Tests\bin\Release\net10.0-windows\SkeletonKey.Runner.Core.Tests.dll"
if (-not (Test-Path -LiteralPath $pluginSource -PathType Leaf)) { throw "Phase 26 plugin fault fixture was not built." }
$pluginAssembly = Join-Path $pluginRoot "SkeletonKey.Runner.Core.Tests.dll"
Copy-Item -LiteralPath $pluginSource -Destination $pluginAssembly
$pluginManifest = [ordered]@{
    schemaVersion = "0.1"
    id = "phase22.fixture"
    version = "1.0.0"
    assembly = "SkeletonKey.Runner.Core.Tests.dll"
    entryType = "SkeletonKey.Runner.Core.Tests.Phase22FixturePlugin"
    sha256 = ('0' * 64)
}
$pluginManifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pluginRoot "phase22.fixture.skeletonkey-plugin.json") -Encoding utf8
$pluginOutput = @(& $runner plugins --plugin-directory $pluginRoot)
$pluginExit = $LASTEXITCODE
Assert-FailedEnvelope -Output $pluginOutput -ExitCode $pluginExit -ExpectedCode "SKP2205" -Scenario "Tampered plugin"
Write-Host "Fault injection passed: tampered plugin rejected with SKP2205."

# Fault 3: a byte is appended to one published payload file. The release verifier must reject it.
$packageRoot = Join-Path $faultRoot "package"
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -Path (Join-Path $published '*') -Destination $packageRoot -Recurse -Force
$packageManifest = Get-Content -LiteralPath (Join-Path $packageRoot "manifest.json") -Raw | ConvertFrom-Json
$payloadEntry = @($packageManifest.files | Where-Object { $_.path -eq 'skeletonkey.exe' }) | Select-Object -First 1
if ($null -eq $payloadEntry) { $payloadEntry = @($packageManifest.files)[0] }
$payloadPath = Join-Path $packageRoot ([string]$payloadEntry.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
$stream = [IO.File]::Open($payloadPath, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $stream.WriteByte(0x26) } finally { $stream.Dispose() }
$rejected = $false
try {
    & (Join-Path $PSScriptRoot "verify-release-package.ps1") $packageRoot -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $ExpectedVersion -RequireSelfContained
}
catch {
    $rejected = $true
}
if (-not $rejected) { throw "Tampered release payload unexpectedly passed integrity verification." }
Write-Host "Fault injection passed: tampered release payload rejected by manifest/SHA-256 gate."

Write-Host "Phase 26 published-binary fault injection passed."
$global:LASTEXITCODE = 0
