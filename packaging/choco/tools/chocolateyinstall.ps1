$ErrorActionPreference = 'Stop'

# These tokens are replaced at pack time by build-choco.ps1.
$version      = '__VERSION__'
$checksumX64  = '__CHECKSUM_X64__'
$checksumArm64 = '__CHECKSUM_ARM64__'

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Native AOT binaries are architecture-specific. Detect the real OS architecture
# (PROCESSOR_ARCHITEW6432 is set when a 32-bit process runs on a 64-bit OS).
$arch = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }

if ($arch -eq 'ARM64') {
    $url      = "https://github.com/jchas2/taskmon/releases/download/v$version/taskmon-$version-windows-arm64.zip"
    $checksum = $checksumArm64
} else {
    $url      = "https://github.com/jchas2/taskmon/releases/download/v$version/taskmon-$version-windows-x64.zip"
    $checksum = $checksumX64
}

$packageArgs = @{
    packageName    = 'taskmon'
    unzipLocation  = $toolsDir
    url64bit       = $url
    checksum64     = $checksum
    checksumType64 = 'sha256'
}

# Extracts taskmon.exe into the tools directory; Chocolatey auto-shims it onto PATH.
Install-ChocolateyZipPackage @packageArgs
