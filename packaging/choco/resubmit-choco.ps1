<#
.SYNOPSIS
    Rebuilds and pushes a specific taskmon Chocolatey package version for moderator resubmission.

.DESCRIPTION
    Downloads the published SHA256 checksums from the GitHub Release for the specified version,
    rebuilds the .nupkg via build-choco.ps1, then pushes it to the Chocolatey Community Repository.

    Use this when a package has been rejected and needs to be resubmitted with the same version number.

.PARAMETER Version
    The version number to resubmit (e.g. 1.7.7).

.PARAMETER ApiKey
    Chocolatey API key. If omitted the CHOCO_API_KEY environment variable is used.

.EXAMPLE
    ./resubmit-choco.ps1 -Version 1.7.7
    ./resubmit-choco.ps1 -Version 1.7.8 -ApiKey "your-api-key"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$ApiKey = $env:CHOCO_API_KEY
)

$ErrorActionPreference = 'Stop'

if (-not $ApiKey) {
    throw "Provide -ApiKey or set the CHOCO_API_KEY environment variable."
}

$baseUrl = "https://github.com/jchas2/taskmon/releases/download/v$Version"

function Get-PublishedSha256([string]$fileName) {
    $url = "$baseUrl/$fileName"
    Write-Host "  Fetching $url"
    $raw = (Invoke-WebRequest -Uri $url -UseBasicParsing).Content.Trim()
    # Format: "<HASH>  <filename>" - take the first token.
    return ($raw -split '\s+')[0].ToLowerInvariant()
}

Write-Host "Fetching published checksums for v$Version..."
$x64Sha256   = Get-PublishedSha256 "taskmon-$Version-windows-x64.zip.sha256"
$arm64Sha256 = Get-PublishedSha256 "taskmon-$Version-windows-arm64.zip.sha256"

Write-Host "  x64   : $x64Sha256"
Write-Host "  arm64 : $arm64Sha256"

# Build the package into packaging/choco/dist/
$buildScript = Join-Path $PSScriptRoot 'build-choco.ps1'
& $buildScript -Version $Version -X64Sha256 $x64Sha256 -Arm64Sha256 $arm64Sha256
if ($LASTEXITCODE -ne 0) { throw "build-choco.ps1 failed." }

$nupkg = Get-ChildItem (Join-Path $PSScriptRoot "dist\taskmon.$Version.nupkg") -ErrorAction SilentlyContinue
if (-not $nupkg) {
    $nupkg = Get-ChildItem (Join-Path $PSScriptRoot 'dist\*.nupkg') |
             Where-Object { $_.Name -like "*$Version*" } |
             Select-Object -First 1
}
if (-not $nupkg) { throw "Could not find .nupkg for version $Version in dist\." }

Write-Host "Pushing $($nupkg.Name) to Chocolatey Community Repository..."
choco push $nupkg.FullName --source https://push.chocolatey.org/ --api-key $ApiKey
if ($LASTEXITCODE -ne 0) { throw "choco push failed with exit code $LASTEXITCODE" }

Write-Host "Done. $($nupkg.Name) submitted for moderator review."
