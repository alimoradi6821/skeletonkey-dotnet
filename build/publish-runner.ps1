param(
    [Parameter(Position = 0)]
    [string] $RuntimeIdentifier = "win-x64",

    [Parameter(Position = 1)]
    [string] $OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\SkeletonKey.Runner\SkeletonKey.Runner.csproj"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot "..\artifacts\runner\$RuntimeIdentifier"
}

dotnet publish $project --configuration Release --runtime $RuntimeIdentifier --self-contained false --output $OutputDirectory
