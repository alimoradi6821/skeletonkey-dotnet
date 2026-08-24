param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PublishedDirectory,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0",

    [string] $OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$published = (Resolve-Path -LiteralPath $PublishedDirectory).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repo "artifacts\agent"
}

if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]*$') { throw "Version contains unsupported Agent bundle filename characters." }

& (Join-Path $PSScriptRoot "verify-release-package.ps1") $published -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version -RequireSelfContained
if (-not $?) { throw "Published Runner is not eligible for Agent bundling." }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$name = "skeletonkey-agent-runtime-$Version-$RuntimeIdentifier"
$bundleRoot = Join-Path $OutputDirectory "$name.content"
$zipPath = Join-Path $OutputDirectory "$name.zip"
$hashPath = Join-Path $OutputDirectory "$name.zip.sha256"

foreach ($path in @($bundleRoot, $zipPath, $hashPath)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}

$runtime = Join-Path $bundleRoot "runtime"
$workflows = Join-Path $bundleRoot "workflows"
$locators = Join-Path $bundleRoot "locators"
$plugins = Join-Path $bundleRoot "plugins"
New-Item -ItemType Directory -Path $runtime, $workflows, $locators, $plugins -Force | Out-Null
"Local plugins must use explicit hash-verified closed manifests. Remote discovery is disabled." | Set-Content -LiteralPath (Join-Path $plugins "POLICY.txt") -Encoding ascii
Copy-Item -Path (Join-Path $published '*') -Destination $runtime -Recurse -Force

Copy-Item -LiteralPath (Join-Path $repo "tests\fixtures\validation\valid-minimal.workflow.json") -Destination (Join-Path $workflows "canary-core.workflow.json") -Force
Copy-Item -LiteralPath (Join-Path $repo "tests\fixtures\soak\phase-028-browser-soak.workflow.json") -Destination (Join-Path $workflows "canary-browser.workflow.json") -Force
Copy-Item -LiteralPath (Join-Path $repo "tests\fixtures\soak\phase-028-browser-soak.locators.json") -Destination (Join-Path $locators "phase-028-browser-soak.locators.json") -Force
Copy-Item -LiteralPath (Join-Path $repo "tests\fixtures\deployment\phase-029-safe-recovery.workflow.json") -Destination (Join-Path $workflows "recovery-safe.workflow.json") -Force
Copy-Item -LiteralPath (Join-Path $repo "tests\fixtures\deployment\phase-029-interrupted.workflow.json") -Destination (Join-Path $workflows "recovery-interrupted.workflow.json") -Force
Copy-Item -LiteralPath (Join-Path $repo "tests\fixtures\deployment\phase-029-deployment.locators.json") -Destination (Join-Path $locators "phase-029-deployment.locators.json") -Force

$runtimeConfig = [ordered]@{
    formatVersion = "0.1"
    product = "SkeletonKey Agent Runtime"
    version = $Version
    runtimeIdentifier = $RuntimeIdentifier
    executable = "runtime/skeletonkey.exe"
    workflowDirectory = "workflows"
    locatorDirectory = "locators"
    pluginDirectory = "plugins"
    durableState = [ordered]@{
        checkpointDirectory = "state/checkpoints"
        artifactDirectory = "state/artifacts"
        logDirectory = "state/logs"
    }
    deployment = [ordered]@{
        mode = "blue-green"
        activation = "pointer-file"
        failClosedOnInterruptedNode = $true
    }
}
$runtimeConfig | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $bundleRoot "agent-runtime.json") -Encoding utf8

$fileEntries = @(Get-ChildItem -LiteralPath $bundleRoot -File -Recurse |
    Sort-Object FullName |
    ForEach-Object {
        [pscustomobject]@{
            path = $_.FullName.Substring($bundleRoot.Length).TrimStart('\').Replace('\', '/')
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })

$runner = Join-Path $runtime "skeletonkey.exe"
$runtimeManifest = Join-Path $runtime "manifest.json"
$bundleManifest = [ordered]@{
    formatVersion = "0.1"
    product = "SkeletonKey Agent Runtime"
    version = $Version
    runtimeIdentifier = $RuntimeIdentifier
    createdUtc = [DateTimeOffset]::UtcNow.ToString("O")
    runtimeExecutableSha256 = (Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash.ToLowerInvariant()
    runtimeManifestSha256 = (Get-FileHash -LiteralPath $runtimeManifest -Algorithm SHA256).Hash.ToLowerInvariant()
    pluginPolicy = [ordered]@{
        mode = "explicit-local-closed-manifest"
        hashVerificationRequired = $true
        remoteDiscoveryAllowed = $false
    }
    files = $fileEntries
}
$bundleManifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $bundleRoot "agent-bundle.json") -Encoding utf8

& (Join-Path $PSScriptRoot "verify-agent-bundle.ps1") $bundleRoot -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version
if (-not $?) { throw "Prepared Agent bundle failed verification." }

Compress-Archive -Path (Join-Path $bundleRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $hashPath -Encoding ascii

Write-Host "Agent bundle archive: $zipPath"
Write-Host "Agent bundle checksum: $hashPath"
