param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $InstallRoot,

    [Parameter(Mandatory = $true, Position = 1)]
    [string] $TaskFile,

    [string] $SlotOverride = ""
)

$ErrorActionPreference = "Stop"
$install = [IO.Path]::GetFullPath($InstallRoot)
$taskPath = (Resolve-Path -LiteralPath $TaskFile).Path
$task = Get-Content -LiteralPath $taskPath -Raw | ConvertFrom-Json
if ($task.formatVersion -ne "0.1") { throw "Unsupported Agent task format." }
if ([string]::IsNullOrWhiteSpace([string] $task.taskId)) { throw "Agent taskId is required." }
if ([string]::IsNullOrWhiteSpace([string] $task.executionId)) { throw "Agent executionId is required." }
$operation = [string] $task.operation
if ($operation -notin @("run", "resume")) { throw "Agent task operation must be run or resume." }

$statePath = Join-Path $install "deployment-state.json"
if ([string]::IsNullOrWhiteSpace($SlotOverride)) {
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) { throw "Agent deployment state is missing." }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $slot = [string] $state.activeSlot
}
else {
    $slot = $SlotOverride
}
if ($slot -notin @("blue", "green")) { throw "Agent active slot is invalid." }
$slotRoot = [IO.Path]::GetFullPath((Join-Path $install "slots\$slot"))
$slotManifestPath = Join-Path $slotRoot "agent-bundle.json"
if (-not (Test-Path -LiteralPath $slotManifestPath -PathType Leaf)) { throw "Active Agent slot manifest is missing." }
$slotManifest = Get-Content -LiteralPath $slotManifestPath -Raw | ConvertFrom-Json
if ($null -ne $state -and -not [string]::IsNullOrWhiteSpace([string] $state.activeBundleManifestSha256)) {
    $actualManifestHash = (Get-FileHash -LiteralPath $slotManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualManifestHash -ne ([string] $state.activeBundleManifestSha256).ToLowerInvariant()) { throw "Active Agent slot manifest integrity check failed." }
}
if ($null -ne $task.requiredVersion -and -not [string]::IsNullOrWhiteSpace([string] $task.requiredVersion) -and [string] $slotManifest.version -ne [string] $task.requiredVersion) {
    throw "Agent task requires runtime version $($task.requiredVersion), but active slot is $($slotManifest.version)."
}

function Resolve-SlotRelativePath {
    param(
        [string] $RelativePath,
        [string] $AllowedDirectory
    )
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "Agent task paths must be non-empty relative paths."
    }
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path $slotRoot $AllowedDirectory))
    $allowedPrefix = $allowedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath((Join-Path $slotRoot $RelativePath))
    $isAllowedRoot = $full.Equals($allowedRoot, [StringComparison]::OrdinalIgnoreCase)
    $isAllowedChild = $full.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)
    if (-not ($isAllowedRoot -or $isAllowedChild)) {
        throw "Agent task path escaped the allowed '$AllowedDirectory' directory."
    }
    return $full
}

$runner = Join-Path $slotRoot "runtime\skeletonkey.exe"
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) { throw "Active Agent slot has no skeletonkey.exe." }
$actualRunnerHash = (Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualRunnerHash -ne ([string] $slotManifest.runtimeExecutableSha256).ToLowerInvariant()) { throw "Active Agent Runner integrity check failed." }
$workflow = Resolve-SlotRelativePath ([string] $task.workflow) "workflows"
if (-not (Test-Path -LiteralPath $workflow -PathType Leaf)) { throw "Agent task workflow is missing." }

$checkpointDirectory = Join-Path $install "state\checkpoints"
New-Item -ItemType Directory -Path $checkpointDirectory -Force | Out-Null
$args = @($operation, "--file", $workflow, "--execution-id", [string] $task.executionId, "--checkpoint-directory", $checkpointDirectory, "--format", "json")

if ($null -ne $task.locatorDirectory -and -not [string]::IsNullOrWhiteSpace([string] $task.locatorDirectory)) {
    $locatorDirectory = Resolve-SlotRelativePath ([string] $task.locatorDirectory) "locators"
    if (-not (Test-Path -LiteralPath $locatorDirectory -PathType Container)) { throw "Agent locator directory is missing." }
    $args += @("--locator-directory", $locatorDirectory)
}
if ($null -ne $task.pluginDirectory -and -not [string]::IsNullOrWhiteSpace([string] $task.pluginDirectory)) {
    $pluginDirectory = Resolve-SlotRelativePath ([string] $task.pluginDirectory) "plugins"
    if (-not (Test-Path -LiteralPath $pluginDirectory -PathType Container)) { throw "Agent plugin directory is missing." }
    $args += @("--plugin-directory", $pluginDirectory)
}
if ($null -ne $task.inputs) {
    $inputs = $task.inputs | ConvertTo-Json -Compress -Depth 32
    $args += @("--inputs", $inputs)
}

$startedUtc = [DateTimeOffset]::UtcNow
$rawLines = @(& $runner @args 2>&1 | ForEach-Object { [string] $_ })
$runnerExitCode = $LASTEXITCODE
$finishedUtc = [DateTimeOffset]::UtcNow
$raw = ($rawLines -join [Environment]::NewLine).Trim()
$runnerEnvelope = $null
if (-not [string]::IsNullOrWhiteSpace($raw)) {
    try { $runnerEnvelope = $raw | ConvertFrom-Json } catch { }
}
$status = if ($null -ne $runnerEnvelope -and $null -ne $runnerEnvelope.status) { [string] $runnerEnvelope.status } elseif ($runnerExitCode -eq 0) { "Succeeded" } else { "Failed" }
$agentEnvelope = [ordered]@{
    formatVersion = "0.1"
    taskId = [string] $task.taskId
    executionId = [string] $task.executionId
    operation = $operation
    slot = $slot
    startedUtc = $startedUtc.ToString("O")
    finishedUtc = $finishedUtc.ToString("O")
    runnerExitCode = $runnerExitCode
    status = $status
    runner = $runnerEnvelope
    rawRunnerOutput = if ($null -eq $runnerEnvelope) { $raw } else { $null }
}
$agentEnvelope | ConvertTo-Json -Depth 32 -Compress | Write-Output
exit $runnerExitCode
