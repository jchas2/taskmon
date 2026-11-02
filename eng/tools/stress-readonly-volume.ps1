# Mounts a small, nearly full VHD, for checking that the HEALTH view ignores read-only volumes. See
# "Stress Testing" in README.md. Needs an elevated PowerShell.
#
# Usage (from the repo root):
#   .\eng\tools\stress-readonly-volume.ps1 [-Seconds 120] [-Writable]
#
# -Writable  Mounts the same volume as writable, which HEALTH should flag. Run it this way as well,
#            to show that it's the read-only state that makes the difference.
#
# The VHD lives in the temp folder and is removed when the time is up or on Ctrl+C.

[CmdletBinding(PositionalBinding=$false)]
Param(
    [ValidateRange(1, 3600)][int] $Seconds = 120,
    [switch] $Writable
)

$ErrorActionPreference = "Stop"

$SizeMb           = 64
$LeaveFreeRatio   = 0.03
$MountWaitSeconds = 15

$identity = [System.Security.Principal.WindowsPrincipal][System.Security.Principal.WindowsIdentity]::GetCurrent()

if (-not $identity.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This needs an elevated PowerShell, as mounting a VHD needs administrator rights."
    exit 1
}

$vhd    = Join-Path ([System.IO.Path]::GetTempPath()) "taskmon-readonly-$PID.vhd"
$script = Join-Path ([System.IO.Path]::GetTempPath()) "taskmon-readonly-$PID.txt"

function Get-MountedRoot {
    for ($waited = 0; $waited -lt $MountWaitSeconds; $waited++) {
        $letter = Get-DiskImage -ImagePath $vhd | Get-Disk | Get-Partition |
            Where-Object { $_.DriveLetter } | Select-Object -First 1 -ExpandProperty DriveLetter

        if ($letter) {
            return "${letter}:\"
        }

        Start-Sleep -Seconds 1
    }

    throw "The VHD mounted without a drive letter."
}

function Test-Writable([string] $root) {
    $probe = Join-Path $root "probe.tmp"

    try {
        [System.IO.File]::WriteAllText($probe, "probe")
        [System.IO.File]::Delete($probe)
        return $true
    }
    catch [System.IO.IOException], [System.UnauthorizedAccessException] {
        return $false
    }
}

try {
    Write-Host "Creating a $SizeMb MB VHD at $vhd"

    Set-Content -Path $script -Value "create vdisk file=`"$vhd`" maximum=$SizeMb type=fixed" -Encoding Ascii
    & diskpart.exe /s $script | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "diskpart couldn't create the VHD, exit code $LASTEXITCODE."
    }

    Mount-DiskImage -ImagePath $vhd -Access ReadWrite | Out-Null

    $disk = Get-DiskImage -ImagePath $vhd | Get-Disk
    Initialize-Disk -Number $disk.Number -PartitionStyle MBR

    # Formatted before it gets a letter, or Explorer offers to format it too.
    $partition = New-Partition -DiskNumber $disk.Number -UseMaximumSize
    $partition | Format-Volume -FileSystem NTFS -NewFileSystemLabel "TASKMON-RO" -Confirm:$false | Out-Null
    Add-PartitionAccessPath -DiskNumber $disk.Number -PartitionNumber $partition.PartitionNumber -AssignDriveLetter

    $root  = Get-MountedRoot
    $drive = New-Object System.IO.DriveInfo($root)
    $fill  = $drive.AvailableFreeSpace - [long]($drive.TotalSize * $LeaveFreeRatio)

    $stream = [System.IO.File]::Open((Join-Path $root "fill.bin"), [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)

    try {
        $stream.SetLength($fill)
    }
    finally {
        $stream.Dispose()
    }

    Write-VolumeCache -DriveLetter $root[0]

    if (-not $Writable) {
        Dismount-DiskImage -ImagePath $vhd | Out-Null
        Mount-DiskImage -ImagePath $vhd -Access ReadOnly | Out-Null
        $root = Get-MountedRoot
    }

    $drive = New-Object System.IO.DriveInfo($root)

    Write-Host ("{0} is {1}, {2:0.0}% free, and {3}." -f
        $root,
        $drive.DriveType,
        (100.0 * $drive.AvailableFreeSpace / $drive.TotalSize),
        $(if (Test-Writable $root) { "accepts writes" } else { "refuses writes" }))

    if ($Writable) {
        Write-Host "Expected in HEALTH: the Volumes row goes Critical for $root within 30 seconds."
    }
    else {
        Write-Host "Expected in HEALTH: nothing for $root at all."
    }

    Write-Host "Holding for $Seconds seconds. Ctrl+C stops it early."
    Start-Sleep -Seconds $Seconds
}
finally {
    if (Test-Path $vhd) {
        Dismount-DiskImage -ImagePath $vhd -ErrorAction SilentlyContinue | Out-Null
        Remove-Item $vhd -Force -ErrorAction SilentlyContinue
    }

    Remove-Item $script -Force -ErrorAction SilentlyContinue
    Write-Host "Removed the VHD."
}
