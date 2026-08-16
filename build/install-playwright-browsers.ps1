param(
    [Parameter(Position = 0)]
    [ValidateSet("chromium", "firefox", "webkit", "all")]
    [string] $Browser = "chromium"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\tools\SkeletonKey.Playwright.BrowserInstaller\SkeletonKey.Playwright.BrowserInstaller.csproj"
dotnet build $project --configuration Release

$installer = Join-Path $PSScriptRoot "..\tools\SkeletonKey.Playwright.BrowserInstaller\bin\Release\net10.0\skeletonkey.playwright-installer.dll"
dotnet $installer $Browser
