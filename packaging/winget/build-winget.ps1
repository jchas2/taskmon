<#
.SYNOPSIS
    Builds the taskmon winget manifests (version + installer + locale).

.DESCRIPTION
    Stamps the version, release date, and per-architecture SHA256 checksums into staging
    copies of the manifest templates, ready to be validated locally or submitted to the
    Windows Package Manager Community Repository (microsoft/winget-pkgs) via wingetcreate.

    Checksums can be supplied directly, or computed from the release .zip archives.

    Used by the release workflow (.github/workflows/release.yml) and for local testing.

.EXAMPLE
    # CI: compute checksums from the downloaded release archives
    ./build-winget.ps1 -Version 1.3.0 -X64Zip path\to\x64.zip -Arm64Zip path\to\arm64.zip

.EXAMPLE
    # Provide checksums directly
    ./build-winget.ps1 -Version 1.3.0 -X64Sha256 ABC... -Arm64Sha256 DEF...
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$X64Zip,
    [string]$Arm64Zip,
    [string]$X64Sha256,
    [string]$Arm64Sha256,
    [string]$ReleaseDate,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$path) {
    if (-not (Test-Path $path)) { throw "Archive not found: $path" }
    (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToUpperInvariant()
}

if (-not $X64Sha256) {
    if (-not $X64Zip) { throw "Provide -X64Sha256 or -X64Zip" }
    $X64Sha256 = Get-Sha256 $X64Zip
}
if (-not $Arm64Sha256) {
    if (-not $Arm64Zip) { throw "Provide -Arm64Sha256 or -Arm64Zip" }
    $Arm64Sha256 = Get-Sha256 $Arm64Zip
}

# winget's schema accepts upper- or lower-case hex; normalise to upper to match wingetcreate.
$X64Sha256 = $X64Sha256.ToUpperInvariant()
$Arm64Sha256 = $Arm64Sha256.ToUpperInvariant()

if (-not $ReleaseDate) { $ReleaseDate = (Get-Date -Format 'yyyy-MM-dd') }

Write-Host "Building winget manifests for taskmon $Version"
Write-Host "  release date : $ReleaseDate"
Write-Host "  x64   sha256 : $X64Sha256"
Write-Host "  arm64 sha256 : $Arm64Sha256"

$templates = Join-Path $PSScriptRoot 'templates'
if (Test-Path $OutputDirectory) { Remove-Item $OutputDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null

# Stamp each template into the output (manifest) directory so the source templates keep
# their __TOKEN__ placeholders.
Get-ChildItem (Join-Path $templates '*.yaml') | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content.Replace('__VERSION__', $Version)
    $content = $content.Replace('__RELEASE_DATE__', $ReleaseDate)
    $content = $content.Replace('__SHA_X64__', $X64Sha256)
    $content = $content.Replace('__SHA_ARM64__', $Arm64Sha256)
    Set-Content -Path (Join-Path $OutputDirectory $_.Name) -Value $content -Encoding UTF8
}

Write-Host "Manifests written to $OutputDirectory"
