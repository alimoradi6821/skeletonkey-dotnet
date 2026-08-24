param(
    [string] $Solution = "SkeletonKey.sln"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    $solutionPath = Join-Path $repo $Solution
    if (-not (Test-Path -LiteralPath $solutionPath)) {
        throw "Solution was not found: $solutionPath"
    }

    $output = & dotnet list $solutionPath package --vulnerable --include-transitive --format json
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet vulnerability audit failed with exit code $LASTEXITCODE."
    }

    $json = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw "NuGet vulnerability audit returned no JSON output."
    }

    $audit = $json | ConvertFrom-Json
    $findings = @()
    foreach ($project in @($audit.projects)) {
        foreach ($framework in @($project.frameworks)) {
            $packages = @($framework.topLevelPackages) + @($framework.transitivePackages)
            foreach ($package in $packages) {
                foreach ($vulnerability in @($package.vulnerabilities)) {
                    if ($null -eq $vulnerability) { continue }
                    $findings += [pscustomobject]@{
                        project = $project.path
                        framework = $framework.framework
                        package = $package.id
                        resolvedVersion = $package.resolvedVersion
                        severity = $vulnerability.severity
                        advisoryUrl = $vulnerability.advisoryUrl
                    }
                }
            }
        }
    }

    if ($findings.Count -gt 0) {
        $findings | Format-Table -AutoSize | Out-String | Write-Host
        throw "Production security gate failed: $($findings.Count) vulnerable NuGet package finding(s) detected."
    }

    Write-Host "NuGet vulnerability audit passed: no vulnerable top-level or transitive packages reported."
}
finally {
    Pop-Location
}
