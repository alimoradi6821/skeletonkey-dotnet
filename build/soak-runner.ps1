param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PublishedDirectory,

    [string] $RuntimeIdentifier = "win-x64",

    [ValidateRange(1, 10000)]
    [int] $CoreIterations = 200,

    [ValidateRange(1, 1000)]
    [int] $BrowserIterations = 30,

    [ValidateRange(0, 100)]
    [int] $WarmupIterations = 3,

    [ValidateRange(0, 180)]
    [int] $MinimumSoakMinutes = 5,

    [ValidateRange(64, 4096)]
    [int] $MaximumPeakWorkingSetMb = 768,

    [ValidateRange(16, 2048)]
    [int] $MaximumMedianWorkingSetGrowthMb = 192,

    [ValidateRange(8, 2048)]
    [int] $MaximumMedianHandleGrowth = 96,

    [string] $OutputPath
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$published = (Resolve-Path $PublishedDirectory).Path
$runner = Join-Path $published "skeletonkey.exe"
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw "Published runner was not found: $runner"
}

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
    throw "Phase 28 soak validation must run on Windows."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repo "artifacts\soak\phase-028-soak-report.json"
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item $outputDirectory -ItemType Directory -Force | Out-Null

$coreWorkflow = Join-Path $repo "tests\fixtures\validation\valid-minimal.workflow.json"
$browserWorkflow = Join-Path $repo "tests\fixtures\soak\phase-028-browser-soak.workflow.json"
$locatorDirectory = Join-Path $repo "tests\fixtures\soak"

foreach ($required in @($coreWorkflow, $browserWorkflow)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Phase 28 fixture is missing: $required"
    }
}

function Invoke-ObservedRunner {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $Kind,

        [Parameter(Mandatory = $true)]
        [int] $Iteration
    )

    function ConvertTo-ProcessArgument {
        param([string] $Value)

        if ($Value -notmatch '[\s"]') {
            return $Value
        }

        $escaped = $Value.Replace('"', '\"')
        if ($escaped.EndsWith('\')) {
            $escaped += '\'
        }

        return '"' + $escaped + '"'
    }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $runner
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join " ")

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $startedUtc = [DateTimeOffset]::UtcNow
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $maximumHandles = 0
    $maximumObservedWorkingSetBytes = [int64] 0

    try {
        if (-not $process.Start()) {
            throw "Could not start skeletonkey.exe."
        }

        try {
            $process.Refresh()
            $maximumHandles = [Math]::Max($maximumHandles, $process.HandleCount)
            $maximumObservedWorkingSetBytes = [Math]::Max($maximumObservedWorkingSetBytes, [int64] $process.WorkingSet64)
        }
        catch {
            # Very short core runs can exit immediately after Start.
        }

        while (-not $process.WaitForExit(25)) {
            try {
                $process.Refresh()
                $maximumHandles = [Math]::Max($maximumHandles, $process.HandleCount)
                $maximumObservedWorkingSetBytes = [Math]::Max($maximumObservedWorkingSetBytes, [int64] $process.WorkingSet64)
            }
            catch {
                # The process can exit between WaitForExit and Refresh. Final exit state is authoritative.
            }
        }

        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        $stopwatch.Stop()

        try {
            $process.Refresh()
            $peakWorkingSetBytes = [Math]::Max($maximumObservedWorkingSetBytes, [int64] $process.PeakWorkingSet64)
        }
        catch {
            $peakWorkingSetBytes = $maximumObservedWorkingSetBytes
        }

        if ($process.ExitCode -ne 0) {
            throw "Phase 28 $Kind iteration $Iteration failed with exit code $($process.ExitCode). stdout: $stdout stderr: $stderr"
        }

        return [pscustomobject]@{
            kind = $Kind
            iteration = $Iteration
            startedUtc = $startedUtc.ToString("O")
            durationMilliseconds = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
            peakWorkingSetBytes = $peakWorkingSetBytes
            maximumHandleCount = $maximumHandles
            exitCode = $process.ExitCode
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-Median {
    param([object[]] $Values)

    if ($Values.Count -eq 0) {
        return 0.0
    }

    $sorted = @($Values | ForEach-Object { [double] $_ } | Sort-Object)
    $middle = [int] [Math]::Floor($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 1) {
        return [double] $sorted[$middle]
    }

    return ([double] $sorted[$middle - 1] + [double] $sorted[$middle]) / 2.0
}

function Get-BrowserProcessIds {
    $ids = @()
    foreach ($name in @("chrome.exe", "chromium.exe", "headless_shell.exe")) {
        try {
            $processes = Get-CimInstance Win32_Process -Filter "Name = '$name'" -ErrorAction Stop
            foreach ($process in $processes) {
                $commandLine = [string] $process.CommandLine
                if ($commandLine.IndexOf("--remote-debugging-pipe", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $ids += [int] $process.ProcessId
                }
            }
        }
        catch {
            # No process with this image name, or CIM temporarily unavailable.
        }
    }

    return @($ids | Sort-Object -Unique)
}

function Wait-ForNoNewBrowserProcesses {
    param(
        [int[]] $BaselineIds,
        [int] $TimeoutSeconds = 12
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $current = @(Get-BrowserProcessIds)
        $remaining = @($current | Where-Object { $BaselineIds -notcontains $_ })
        if ($remaining.Count -eq 0) {
            return @()
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    $current = @(Get-BrowserProcessIds)
    return @($current | Where-Object { $BaselineIds -notcontains $_ })
}

function Test-SampleBounds {
    param(
        [object[]] $Samples,
        [string] $Label
    )

    if ($Samples.Count -eq 0) {
        throw "No $Label samples were collected."
    }

    $window = [Math]::Min(5, [Math]::Max(1, [int] [Math]::Floor($Samples.Count / 3)))
    $first = @($Samples | Select-Object -First $window)
    $last = @($Samples | Select-Object -Last $window)

    $firstWorkingSet = Get-Median -Values @($first | ForEach-Object { $_.peakWorkingSetBytes })
    $lastWorkingSet = Get-Median -Values @($last | ForEach-Object { $_.peakWorkingSetBytes })
    $workingSetGrowthMb = [Math]::Max(0.0, ($lastWorkingSet - $firstWorkingSet) / 1MB)
    $maximumPeakBytes = [int64] (($Samples | Measure-Object -Property peakWorkingSetBytes -Maximum).Maximum)
    $maximumPeakMb = ($maximumPeakBytes / 1MB)
    if ($maximumPeakBytes -le 0) {
        throw "$Label working-set sampling produced no usable measurements."
    }

    $firstHandles = Get-Median -Values @($first | ForEach-Object { $_.maximumHandleCount })
    $lastHandles = Get-Median -Values @($last | ForEach-Object { $_.maximumHandleCount })
    $handleGrowth = [Math]::Max(0.0, $lastHandles - $firstHandles)

    if ($maximumPeakMb -gt $MaximumPeakWorkingSetMb) {
        throw "$Label peak working set was $([Math]::Round($maximumPeakMb, 2)) MiB; limit is $MaximumPeakWorkingSetMb MiB."
    }

    if ($workingSetGrowthMb -gt $MaximumMedianWorkingSetGrowthMb) {
        throw "$Label median working-set growth was $([Math]::Round($workingSetGrowthMb, 2)) MiB; limit is $MaximumMedianWorkingSetGrowthMb MiB."
    }

    if ($handleGrowth -gt $MaximumMedianHandleGrowth) {
        throw "$Label median handle growth was $([Math]::Round($handleGrowth, 2)); limit is $MaximumMedianHandleGrowth."
    }

    return [ordered]@{
        iterations = $Samples.Count
        firstWindowSize = $window
        firstMedianWorkingSetBytes = [int64] $firstWorkingSet
        lastMedianWorkingSetBytes = [int64] $lastWorkingSet
        medianWorkingSetGrowthBytes = [int64] [Math]::Max(0.0, $lastWorkingSet - $firstWorkingSet)
        maximumPeakWorkingSetBytes = $maximumPeakBytes
        firstMedianHandleCount = [double] $firstHandles
        lastMedianHandleCount = [double] $lastHandles
        medianHandleGrowth = [double] $handleGrowth
        medianDurationMilliseconds = [double] (Get-Median -Values @($Samples | ForEach-Object { $_.durationMilliseconds }))
        maximumDurationMilliseconds = [double] (($Samples | Measure-Object -Property durationMilliseconds -Maximum).Maximum)
    }
}

$startedUtc = [DateTimeOffset]::UtcNow
$browserBaseline = @(Get-BrowserProcessIds)
$coreSamples = [System.Collections.Generic.List[object]]::new()
$browserSamples = [System.Collections.Generic.List[object]]::new()

for ($iteration = 0; $iteration -lt $WarmupIterations; $iteration++) {
    [void] (Invoke-ObservedRunner -Arguments @(
        "run",
        "--file", $coreWorkflow,
        "--execution-id", "phase-028-warmup-$iteration"
    ) -Kind "warmup" -Iteration $iteration)
}

for ($iteration = 0; $iteration -lt $CoreIterations; $iteration++) {
    $sample = Invoke-ObservedRunner -Arguments @(
        "run",
        "--file", $coreWorkflow,
        "--execution-id", "phase-028-core-$iteration"
    ) -Kind "core" -Iteration $iteration
    $coreSamples.Add($sample)
}

$minimumFinishUtc = $startedUtc.AddMinutes($MinimumSoakMinutes)
$browserIteration = 0
while ($browserIteration -lt $BrowserIterations -or [DateTimeOffset]::UtcNow -lt $minimumFinishUtc) {
    $sample = Invoke-ObservedRunner -Arguments @(
        "run",
        "--file", $browserWorkflow,
        "--locator-directory", $locatorDirectory,
        "--execution-id", "phase-028-browser-$browserIteration"
    ) -Kind "browser" -Iteration $browserIteration
    $browserSamples.Add($sample)
    $browserIteration++
}

$coreSummary = Test-SampleBounds -Samples $coreSamples.ToArray() -Label "Core runner"
$browserSummary = Test-SampleBounds -Samples $browserSamples.ToArray() -Label "Browser runner"
$orphanBrowserProcessIds = @(Wait-ForNoNewBrowserProcesses -BaselineIds $browserBaseline)
if ($orphanBrowserProcessIds.Count -ne 0) {
    throw "Phase 28 detected orphaned Playwright Chromium process(es): $($orphanBrowserProcessIds -join ', ')."
}

$finishedUtc = [DateTimeOffset]::UtcNow
$report = [ordered]@{
    schemaVersion = "0.1"
    phase = 28
    status = "passed"
    runtimeIdentifier = $RuntimeIdentifier
    startedUtc = $startedUtc.ToString("O")
    finishedUtc = $finishedUtc.ToString("O")
    elapsedSeconds = [Math]::Round(($finishedUtc - $startedUtc).TotalSeconds, 3)
    thresholds = [ordered]@{
        maximumPeakWorkingSetMb = $MaximumPeakWorkingSetMb
        maximumMedianWorkingSetGrowthMb = $MaximumMedianWorkingSetGrowthMb
        maximumMedianHandleGrowth = $MaximumMedianHandleGrowth
        orphanPlaywrightChromiumProcesses = 0
    }
    warmupIterations = $WarmupIterations
    minimumSoakMinutes = $MinimumSoakMinutes
    core = $coreSummary
    browser = $browserSummary
    orphanPlaywrightChromiumProcessIds = @()
}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8

Write-Host "Phase 28 soak report: $OutputPath"
Write-Host ("Phase 28 core: {0} iterations, peak {1:N2} MiB, median growth {2:N2} MiB, handle growth {3:N2}." -f `
    $coreSummary.iterations, ($coreSummary.maximumPeakWorkingSetBytes / 1MB), ($coreSummary.medianWorkingSetGrowthBytes / 1MB), $coreSummary.medianHandleGrowth)
Write-Host ("Phase 28 browser: {0} iterations, peak {1:N2} MiB, median growth {2:N2} MiB, handle growth {3:N2}." -f `
    $browserSummary.iterations, ($browserSummary.maximumPeakWorkingSetBytes / 1MB), ($browserSummary.medianWorkingSetGrowthBytes / 1MB), $browserSummary.medianHandleGrowth)
Write-Host "Phase 28 resource-leak and browser-lifecycle soak passed."
$global:LASTEXITCODE = 0
