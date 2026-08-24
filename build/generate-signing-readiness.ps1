param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $PackageDirectory,

    [string] $Version = "0.1.0",

    [string] $RuntimeIdentifier = "win-x64",

    [string] $OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$package = Resolve-Path -LiteralPath $PackageDirectory
$runner = Join-Path $package "skeletonkey.exe"

if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw "Published runner executable was not found: $runner"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repo "artifacts\release\skeletonkey-$Version-$RuntimeIdentifier.signing-readiness.json"
}

$setCommand = Get-Command Set-AuthenticodeSignature -ErrorAction SilentlyContinue
$getCommand = Get-Command Get-AuthenticodeSignature -ErrorAction SilentlyContinue
if ($null -eq $setCommand -or $null -eq $getCommand) {
    throw "Windows Authenticode signing commands are not available in this PowerShell session."
}

$signature = Get-AuthenticodeSignature -FilePath $runner
$status = [string]$signature.Status
if ($status -notin @("NotSigned", "Valid")) {
    throw "Published runner has an unacceptable Authenticode state: $status"
}

$signingState = if ($status -eq "Valid") { "signed" } else { "ready-unsigned" }
$certificate = $null
if ($null -ne $signature.SignerCertificate) {
    $certificate = [ordered]@{
        subject = [string]$signature.SignerCertificate.Subject
        thumbprint = [string]$signature.SignerCertificate.Thumbprint
        notBeforeUtc = $signature.SignerCertificate.NotBefore.ToUniversalTime().ToString("o")
        notAfterUtc = $signature.SignerCertificate.NotAfter.ToUniversalTime().ToString("o")
    }
}

$readiness = [ordered]@{
    formatVersion = "1.0"
    product = "SkeletonKey Runner"
    version = $Version
    runtimeIdentifier = $RuntimeIdentifier
    executable = "skeletonkey.exe"
    executableSha256 = (Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash.ToLowerInvariant()
    state = $signingState
    authenticodeStatus = $status
    hashAlgorithm = "SHA256"
    timestampRequiredForProduction = $true
    productionCertificateRequired = ($status -ne "Valid")
    setAuthenticodeSignatureAvailable = $true
    getAuthenticodeSignatureAvailable = $true
    signerCertificate = $certificate
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
}

$parent = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$readiness | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8

Write-Host "Code-signing readiness: $signingState ($status)"
Write-Host "Code-signing readiness record: $OutputPath"
