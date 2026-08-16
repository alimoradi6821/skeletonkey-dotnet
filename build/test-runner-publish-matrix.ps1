param(
    [string] $OutputRoot = "",
    [string] $RuntimeIdentifier = "win-x64",
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

function New-CleanDirectory {
    param([string] $Path)

    if (Test-Path -LiteralPath $Path) {
        throw "Output directory already exists: $Path"
    }

    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Get-TextFile {
    param([string] $Path)

    if (Test-Path -LiteralPath $Path) {
        return [string](Get-Content -LiteralPath $Path -Raw)
    }

    return ""
}

function Invoke-RunnerCommand {
    param(
        [string] $CommandPath,
        [string] $WorkingDirectory,
        [bool] $UseDotnet
    )

    $stdout = Join-Path $WorkingDirectory "stdout.txt"
    $stderr = Join-Path $WorkingDirectory "stderr.txt"

    Push-Location $WorkingDirectory
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        if ($UseDotnet) {
            & dotnet $CommandPath version 1> $stdout 2> $stderr
        } else {
            & $CommandPath version 1> $stdout 2> $stderr
        }

        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
        Pop-Location
    }

    [pscustomobject]@{
        exitCode = $exitCode
        stdout = Get-TextFile $stdout
        stderr = Get-TextFile $stderr
        startupSucceeded = $exitCode -eq 0
        cetOrLoaderFailure = ((Get-TextFile $stdout) + (Get-TextFile $stderr)) -match "CET|internal error|coreclr|Fatal error"
    }
}

function Get-OutputFiles {
    param([string] $Path)

    $root = (Resolve-Path -LiteralPath $Path).Path.TrimEnd("\")
    Get-ChildItem -LiteralPath $Path -File -Recurse |
        ForEach-Object {
            $relative = $_.FullName.Substring($root.Length).TrimStart("\")
            [pscustomobject]@{
                path = $relative.Replace("\", "/")
                bytes = $_.Length
            }
        } |
        Sort-Object path
}

$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repo "src\SkeletonKey.Runner\SkeletonKey.Runner.csproj"
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repo "artifacts\runner-publish-matrix"
}

New-CleanDirectory $OutputRoot

$matrix = @(
    @{
        name = "framework-dependent-non-single-file-apphost-enabled"
        properties = @{
            SelfContained = "false"
            PublishSingleFile = "false"
            UseAppHost = "true"
        }
        command = "skeletonkey.exe"
        useDotnet = $false
    },
    @{
        name = "framework-dependent-non-single-file-apphost-disabled"
        properties = @{
            SelfContained = "false"
            PublishSingleFile = "false"
            UseAppHost = "false"
        }
        command = "skeletonkey.dll"
        useDotnet = $true
    },
    @{
        name = "framework-dependent-single-file"
        properties = @{
            SelfContained = "false"
            PublishSingleFile = "true"
            UseAppHost = "true"
        }
        command = "skeletonkey.exe"
        useDotnet = $false
    },
    @{
        name = "self-contained-non-single-file"
        properties = @{
            SelfContained = "true"
            PublishSingleFile = "false"
            UseAppHost = "true"
        }
        command = "skeletonkey.exe"
        useDotnet = $false
    },
    @{
        name = "self-contained-single-file"
        properties = @{
            SelfContained = "true"
            PublishSingleFile = "true"
            UseAppHost = "true"
        }
        command = "skeletonkey.exe"
        useDotnet = $false
    },
    @{
        name = "self-contained-single-file-readytorun-disabled"
        properties = @{
            SelfContained = "true"
            PublishSingleFile = "true"
            UseAppHost = "true"
            PublishReadyToRun = "false"
        }
        command = "skeletonkey.exe"
        useDotnet = $false
    },
    @{
        name = "self-contained-single-file-compression-disabled"
        properties = @{
            SelfContained = "true"
            PublishSingleFile = "true"
            UseAppHost = "true"
            EnableCompressionInSingleFile = "false"
        }
        command = "skeletonkey.exe"
        useDotnet = $false
    }
)

$results = @()
foreach ($entry in $matrix) {
    $entryRoot = Join-Path $OutputRoot $entry.name
    $publishRoot = Join-Path $entryRoot "publish"
    New-CleanDirectory $entryRoot
    $entryRoot = (Resolve-Path -LiteralPath $entryRoot).Path
    $publishRoot = Join-Path $entryRoot "publish"
    New-Item -ItemType Directory -Path $publishRoot | Out-Null

    $publishArgs = @(
        "publish",
        $project,
        "--configuration", $Configuration,
        "--runtime", $RuntimeIdentifier,
        "--output", $publishRoot,
        "/p:PublishTrimmed=false"
    )

    foreach ($property in $entry.properties.GetEnumerator()) {
        $publishArgs += "/p:$($property.Key)=$($property.Value)"
    }

    $publishStdout = Join-Path $entryRoot "publish.stdout.txt"
    $publishStderr = Join-Path $entryRoot "publish.stderr.txt"
    & dotnet @publishArgs 1> $publishStdout 2> $publishStderr
    $publishExitCode = $LASTEXITCODE

    $runner = Join-Path $publishRoot $entry.command
    $execution = $null
    if ($publishExitCode -eq 0 -and (Test-Path -LiteralPath $runner)) {
        $runner = (Resolve-Path -LiteralPath $runner).Path
        $execution = Invoke-RunnerCommand -CommandPath $runner -WorkingDirectory $entryRoot -UseDotnet $entry.useDotnet
    }

    $properties = [ordered]@{
        UseAppHost = $entry.properties.UseAppHost
        PublishSingleFile = $entry.properties.PublishSingleFile
        SelfContained = $entry.properties.SelfContained
        PublishReadyToRun = $entry.properties.PublishReadyToRun
        EnableCompressionInSingleFile = $entry.properties.EnableCompressionInSingleFile
        IncludeNativeLibrariesForSelfExtract = $entry.properties.IncludeNativeLibrariesForSelfExtract
        IncludeAllContentForSelfExtract = $entry.properties.IncludeAllContentForSelfExtract
        DebugType = $entry.properties.DebugType
        InvariantGlobalization = $entry.properties.InvariantGlobalization
        PublishTrimmed = "false"
        RuntimeIdentifier = $RuntimeIdentifier
        Configuration = $Configuration
    }

    $results += [pscustomobject]@{
        name = $entry.name
        publishSucceeded = $publishExitCode -eq 0
        publishExitCode = $publishExitCode
        publishStdout = Get-TextFile $publishStdout
        publishStderr = Get-TextFile $publishStderr
        command = $entry.command
        usedDotnetHost = $entry.useDotnet
        commandPath = $runner
        execution = $execution
        outputFiles = @(Get-OutputFiles $publishRoot)
        properties = $properties
    }
}

$os = Get-CimInstance Win32_OperatingSystem
$computer = Get-CimInstance Win32_ComputerSystem
$summary = [pscustomobject]@{
    generatedBy = "build/test-runner-publish-matrix.ps1"
    sdk = (& dotnet --version)
    host = (& dotnet --info)
    os = [pscustomobject]@{
        caption = $os.Caption
        version = $os.Version
        buildNumber = $os.BuildNumber
        architecture = $os.OSArchitecture
        systemType = $computer.SystemType
    }
    results = $results
}

$summaryPath = Join-Path $OutputRoot "publish-matrix.json"
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
$summary | ConvertTo-Json -Depth 8
