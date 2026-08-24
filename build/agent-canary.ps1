param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $BundleArchive,

    [string] $RuntimeIdentifier = "win-x64",

    [string] $Version = "0.1.0",

    [string] $InstallRoot = ""
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $repo "artifacts\canary\phase-029-agent"
}
$install = [IO.Path]::GetFullPath($InstallRoot)
if (Test-Path -LiteralPath $install) { Remove-Item -LiteralPath $install -Recurse -Force }
New-Item -ItemType Directory -Path $install -Force | Out-Null

function Invoke-AgentTaskProcess {
    param(
        [string] $TaskFile,
        [string] $ExpectedSlot
    )

    $invokeScript = Join-Path $PSScriptRoot "invoke-agent-task.ps1"
    $output = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $invokeScript $install $TaskFile 2>&1 | ForEach-Object { [string] $_ })
    $code = $LASTEXITCODE
    $raw = ($output -join [Environment]::NewLine).Trim()
    if ($code -ne 0) { throw "Agent task failed with exit code $code. Output: $raw" }
    $envelope = $raw | ConvertFrom-Json
    if ($envelope.status -ne "Succeeded" -or $envelope.slot -ne $ExpectedSlot) {
        throw "Agent task returned an unexpected canary envelope: $raw"
    }
    return $envelope
}

function Write-Task {
    param(
        [string] $Name,
        [string] $ExecutionId,
        [string] $Workflow,
        [string] $LocatorDirectory = ""
    )

    $path = Join-Path $install "$Name.task.json"
    $task = [ordered]@{
        formatVersion = "0.1"
        taskId = $Name
        executionId = $ExecutionId
        operation = "run"
        requiredVersion = $Version
        workflow = $Workflow
        locatorDirectory = if ([string]::IsNullOrWhiteSpace($LocatorDirectory)) { $null } else { $LocatorDirectory }
        inputs = @{}
    }
    $task | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding utf8
    return $path
}

function Read-CheckpointPayloads {
    param([string] $CheckpointDirectory)
    $result = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $CheckpointDirectory -Filter "checkpoint-*.json" -File -ErrorAction SilentlyContinue)) {
        try {
            $envelope = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
            $payloadBytes = [Convert]::FromBase64String([string] $envelope.payload)
            $payloadJson = [Text.Encoding]::UTF8.GetString($payloadBytes)
            $payload = $payloadJson | ConvertFrom-Json
            $result += [pscustomobject]@{ File = $file.FullName; Payload = $payload }
        }
        catch { }
    }
    return @($result)
}

function Wait-CheckpointStep {
    param(
        [string] $CheckpointDirectory,
        [string] $ExecutionId,
        [string] $NodeId,
        [string] $Status,
        [int] $MinimumRetryAttempt = -1,
        [int] $TimeoutSeconds = 15
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($item in @(Read-CheckpointPayloads $CheckpointDirectory)) {
            $payload = $item.Payload
            if ([string] $payload.executionId -ne $ExecutionId) { continue }
            foreach ($step in @($payload.steps)) {
                if ([string] $step.nodeId -eq $NodeId -and [string] $step.status -eq $Status) {
                    if ($MinimumRetryAttempt -lt 0 -or [int] $step.retryAttempt -ge $MinimumRetryAttempt) {
                        return $item
                    }
                }
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for checkpoint node '$NodeId' status '$Status' for execution '$ExecutionId'."
}

function Start-ObservedRunner {
    param([string[]] $Arguments)

    function ConvertTo-ProcessArgument {
        param([string] $Value)
        if ($Value -notmatch '[\s"]') { return $Value }
        $escaped = $Value.Replace('"', '\"')
        if ($escaped.EndsWith('\')) { $escaped += '\' }
        return '"' + $escaped + '"'
    }

    $state = Get-Content -LiteralPath (Join-Path $install "deployment-state.json") -Raw | ConvertFrom-Json
    $runner = Join-Path $install "slots\$($state.activeSlot)\runtime\skeletonkey.exe"
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $runner
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join " ")
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) { throw "Could not start published Agent runner." }
    return $process
}

function Stop-ProcessTree {
    param([System.Diagnostics.Process] $Process)
    if (-not $Process.HasExited) {
        & taskkill.exe /PID $Process.Id /T /F | Out-Null
        if (-not $Process.WaitForExit(10000)) { throw "Process tree did not exit after taskkill." }
    }
}

$checkpointDirectory = Join-Path $install "state\checkpoints"

# Blue initial install and active-slot task contract.
& (Join-Path $PSScriptRoot "stage-agent-bundle.ps1") $BundleArchive $install -Slot blue -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version
& (Join-Path $PSScriptRoot "promote-agent-slot.ps1") $install -Slot blue -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version
$coreTask = Write-Task "phase-029-core-blue" "phase-029-core-blue" "workflows/canary-core.workflow.json"
[void] (Invoke-AgentTaskProcess $coreTask "blue")
$browserTask = Write-Task "phase-029-browser-blue" "phase-029-browser-blue" "workflows/canary-browser.workflow.json" "locators"
[void] (Invoke-AgentTaskProcess $browserTask "blue")

# Stage inactive green, promote it, verify task routing, then rollback to blue.
& (Join-Path $PSScriptRoot "stage-agent-bundle.ps1") $BundleArchive $install -Slot green -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version
& (Join-Path $PSScriptRoot "promote-agent-slot.ps1") $install -Slot green -RuntimeIdentifier $RuntimeIdentifier -ExpectedVersion $Version
$greenTask = Write-Task "phase-029-core-green" "phase-029-core-green" "workflows/canary-core.workflow.json"
[void] (Invoke-AgentTaskProcess $greenTask "green")
& (Join-Path $PSScriptRoot "rollback-agent-slot.ps1") $install -RuntimeIdentifier $RuntimeIdentifier
$rollbackTask = Write-Task "phase-029-core-rollback" "phase-029-core-rollback" "workflows/canary-core.workflow.json"
[void] (Invoke-AgentTaskProcess $rollbackTask "blue")
$deploymentState = Get-Content -LiteralPath (Join-Path $install "deployment-state.json") -Raw | ConvertFrom-Json
if ($deploymentState.activeSlot -ne "blue" -or $deploymentState.previousSlot -ne "green" -or [int64] $deploymentState.revision -lt 3 -or [string]::IsNullOrWhiteSpace([string] $deploymentState.activeBundleManifestSha256)) {
    throw "Blue/green rollback state is invalid."
}
Write-Host "Phase 29 blue/green stage, promote, task routing, and rollback passed."

# Safe-boundary process + Chromium loss. First wait attempt fails, retry boundary is checkpointed,
# then the entire process tree is killed. Resume must reconstruct the ephemeral page and succeed.
$safeExecution = "phase-029-safe-recovery"
$safeWorkflow = Join-Path $install "slots\blue\workflows\recovery-safe.workflow.json"
$locators = Join-Path $install "slots\blue\locators"
$safeProcess = Start-ObservedRunner @(
    "run", "--file", $safeWorkflow,
    "--locator-directory", $locators,
    "--execution-id", $safeExecution,
    "--checkpoint-directory", $checkpointDirectory,
    "--format", "json"
)
try {
    [void] (Wait-CheckpointStep $checkpointDirectory $safeExecution "wait" "ready" 1 15)
    Stop-ProcessTree $safeProcess
}
finally {
    if (-not $safeProcess.HasExited) { Stop-ProcessTree $safeProcess }
    $safeProcess.Dispose()
}

$activeRunner = Join-Path $install "slots\blue\runtime\skeletonkey.exe"
$resumeOutput = @(& $activeRunner resume --file $safeWorkflow --locator-directory $locators --execution-id $safeExecution --checkpoint-directory $checkpointDirectory --format json 2>&1 | ForEach-Object { [string] $_ })
$resumeCode = $LASTEXITCODE
$resumeRaw = ($resumeOutput -join [Environment]::NewLine).Trim()
if ($resumeCode -ne 0) { throw "Safe-boundary crash recovery failed with exit code $resumeCode. Output: $resumeRaw" }
$resumeEnvelope = $resumeRaw | ConvertFrom-Json
if ($resumeEnvelope.status -ne "Succeeded") { throw "Safe-boundary crash recovery did not succeed: $resumeRaw" }
Write-Host "Phase 29 safe-boundary process/browser-loss resume passed."

# In-flight handler kill must not guess whether side effects occurred. Resume must fail closed with SKR3006.
$interruptedExecution = "phase-029-interrupted"
$interruptedWorkflow = Join-Path $install "slots\blue\workflows\recovery-interrupted.workflow.json"
$interruptedProcess = Start-ObservedRunner @(
    "run", "--file", $interruptedWorkflow,
    "--locator-directory", $locators,
    "--execution-id", $interruptedExecution,
    "--checkpoint-directory", $checkpointDirectory,
    "--format", "json"
)
try {
    [void] (Wait-CheckpointStep $checkpointDirectory $interruptedExecution "wait" "running" -1 15)
    Stop-ProcessTree $interruptedProcess
}
finally {
    if (-not $interruptedProcess.HasExited) { Stop-ProcessTree $interruptedProcess }
    $interruptedProcess.Dispose()
}

$failedResumeOutput = @(& $activeRunner resume --file $interruptedWorkflow --locator-directory $locators --execution-id $interruptedExecution --checkpoint-directory $checkpointDirectory --format json 2>&1 | ForEach-Object { [string] $_ })
$failedResumeCode = $LASTEXITCODE
$failedResumeRaw = ($failedResumeOutput -join [Environment]::NewLine).Trim()
if ($failedResumeCode -eq 0) { throw "Interrupted in-flight resume unexpectedly succeeded." }
if ($failedResumeRaw -notmatch 'SKR3006') { throw "Interrupted in-flight resume did not fail with SKR3006. Output: $failedResumeRaw" }
Write-Host "Phase 29 interrupted in-flight recovery passed: resume failed closed with SKR3006."

$report = [ordered]@{
    formatVersion = "0.1"
    phase = 29
    status = "passed"
    version = $Version
    runtimeIdentifier = $RuntimeIdentifier
    activeSlot = "blue"
    previousSlot = "green"
    deploymentRevision = [int64] $deploymentState.revision
    bundleSha256 = (Get-FileHash -LiteralPath $BundleArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    checks = @(
        "bundle-integrity",
        "blue-green-promotion",
        "agent-task-routing",
        "rollback",
        "safe-boundary-process-browser-recovery",
        "interrupted-handler-skr3006"
    )
    finishedUtc = [DateTimeOffset]::UtcNow.ToString("O")
}
$reportPath = Join-Path $repo "artifacts\canary\phase-029-canary-report.json"
New-Item -ItemType Directory -Path (Split-Path -Parent $reportPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Host "Phase 29 canary report: $reportPath"
Write-Host "Phase 29 deployment, rollback, and crash-recovery canary passed."
