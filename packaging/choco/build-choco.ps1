<#
.SYNOPSIS
    Builds the taskmon Chocolatey package (.nupkg).

.DESCRIPTION
    Stamps the version and per-architecture SHA256 checksums into a staging copy of
    chocolateyinstall.ps1, then runs `choco pack`. Checksums can be supplied directly,
    or computed from the release .zip archives.

    Used by the release workflow (.github/workflows/release.yml) and for local testing.

.EXAMPLE
    # CI: compute checksums from the downloaded release archives
    ./build-choco.ps1 -Version 1.3.0 -X64Zip path\to\x64.zip -Arm64Zip path\to\arm64.zip

.EXAMPLE
    # Provide checksums directly
    ./build-choco.ps1 -Version 1.3.0 -X64Sha256 ABC... -Arm64Sha256 DEF...
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$X64Zip,
    [string]$Arm64Zip,
    [string]$X64Sha256,
    [string]$Arm64Sha256,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$path) {
    if (-not (Test-Path $path)) { throw "Archive not found: $path" }
    (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
}

if (-not $X64Sha256) {
    if (-not $X64Zip) { throw "Provide -X64Sha256 or -X64Zip" }
    $X64Sha256 = Get-Sha256 $X64Zip
}
if (-not $Arm64Sha256) {
    if (-not $Arm64Zip) { throw "Provide -Arm64Sha256 or -Arm64Zip" }
    $Arm64Sha256 = Get-Sha256 $Arm64Zip
}

Write-Host "Packaging taskmon $Version"
Write-Host "  x64   sha256: $X64Sha256"
Write-Host "  arm64 sha256: $Arm64Sha256"

# Stage a clean copy so the source files keep their __TOKEN__ placeholders.
$staging = Join-Path $PSScriptRoot 'staging'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'taskmon.nuspec') $staging
Copy-Item (Join-Path $PSScriptRoot 'tools') $staging -Recurse

$installPath = Join-Path $staging 'tools\chocolateyinstall.ps1'
$content = Get-Content $installPath -Raw
$content = $content.Replace('__VERSION__', $Version)
$content = $content.Replace('__CHECKSUM_X64__', $X64Sha256)
$content = $content.Replace('__CHECKSUM_ARM64__', $Arm64Sha256)
Set-Content -Path $installPath -Value $content -Encoding UTF8

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

choco pack (Join-Path $staging 'taskmon.nuspec') --version $Version --outputdirectory $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "choco pack failed with exit code $LASTEXITCODE" }

Write-Host "Package written to $OutputDirectory"
