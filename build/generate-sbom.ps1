param(
    [string] $Solution = "SkeletonKey.sln",

    [string] $Version = "0.1.0",

    [string] $RuntimeIdentifier = "win-x64",

    [string] $OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    $solutionPath = Join-Path $repo $Solution
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        throw "Solution was not found: $solutionPath"
    }

    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $OutputPath = Join-Path $repo "artifacts\release\skeletonkey-$Version-$RuntimeIdentifier.sbom.cdx.json"
    }

    $output = & dotnet list $solutionPath package --include-transitive --format json
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet package inventory failed with exit code $LASTEXITCODE."
    }

    $json = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw "NuGet package inventory returned no JSON output."
    }

    $inventory = $json | ConvertFrom-Json
    $componentMap = @{}

    foreach ($project in @($inventory.projects)) {
        if ($null -eq $project) { continue }
        $projectPath = [string]$project.path
        foreach ($framework in @($project.frameworks)) {
            if ($null -eq $framework) { continue }
            $frameworkName = [string]$framework.framework

            foreach ($kind in @("top-level", "transitive")) {
                $packages = if ($kind -eq "top-level") { @($framework.topLevelPackages) } else { @($framework.transitivePackages) }
                foreach ($package in $packages) {
                    if ($null -eq $package) { continue }
                    $id = [string]$package.id
                    $resolved = [string]$package.resolvedVersion
                    if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($resolved)) { continue }

                    $key = "$($id.ToLowerInvariant())|$($resolved.ToLowerInvariant())"
                    if (-not $componentMap.ContainsKey($key)) {
                        $purl = "pkg:nuget/$([Uri]::EscapeDataString($id))@$([Uri]::EscapeDataString($resolved))"
                        $componentMap[$key] = [ordered]@{
                            type = "library"
                            'bom-ref' = $purl
                            name = $id
                            version = $resolved
                            purl = $purl
                            projects = @{}
                            scopes = @{}
                        }
                    }

                    $entry = $componentMap[$key]
                    $projectKey = "$projectPath [$frameworkName]"
                    $entry.projects[$projectKey] = $true
                    $entry.scopes[$kind] = $true
                }
            }
        }
    }

    $components = @()
    foreach ($entry in @($componentMap.Values | Sort-Object { $_.name.ToLowerInvariant() }, { $_.version.ToLowerInvariant() })) {
        $projects = @($entry.projects.Keys | Sort-Object)
        $scopes = @($entry.scopes.Keys | Sort-Object)
        $components += [ordered]@{
            type = $entry.type
            'bom-ref' = $entry.'bom-ref'
            name = $entry.name
            version = $entry.version
            purl = $entry.purl
            properties = @(
                [ordered]@{ name = "skeletonkey:dependencyScopes"; value = ($scopes -join ",") },
                [ordered]@{ name = "skeletonkey:projects"; value = ($projects -join ";") }
            )
        }
    }

    if ($components.Count -eq 0) {
        throw "SBOM generation produced no NuGet components."
    }

    $bom = [ordered]@{
        bomFormat = "CycloneDX"
        specVersion = "1.5"
        serialNumber = "urn:uuid:$([Guid]::NewGuid())"
        version = 1
        metadata = [ordered]@{
            timestamp = [DateTimeOffset]::UtcNow.ToString("o")
            tools = [ordered]@{
                components = @(
                    [ordered]@{
                        type = "application"
                        name = "SkeletonKey SBOM Generator"
                        version = $Version
                    }
                )
            }
            component = [ordered]@{
                type = "application"
                'bom-ref' = "pkg:generic/skeletonkey-runner@$([Uri]::EscapeDataString($Version))?arch=$RuntimeIdentifier"
                name = "SkeletonKey Runner"
                version = $Version
            }
            properties = @(
                [ordered]@{ name = "skeletonkey:runtimeIdentifier"; value = $RuntimeIdentifier },
                [ordered]@{ name = "skeletonkey:dependencySource"; value = "dotnet list package --include-transitive --format json" }
            )
        }
        components = $components
    }

    $parent = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $bom | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8

    Write-Host "CycloneDX SBOM: $OutputPath"
    Write-Host "CycloneDX components: $($components.Count)"
}
finally {
    Pop-Location
}
