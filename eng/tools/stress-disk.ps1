# Keeps a disk busy, for trying the Disk rules in the HEALTH view. See "Stress Testing" in README.md.
#
# Usage (from the repo root):
#   .\eng\tools\stress-disk.ps1 [-Seconds 120] [-Path D:\] [-FileGb 4] [-Threads 64] [-BlockKb 4096]
#                               [-ReadPercent 30]
#
# -Path         A folder on the disk to test. Defaults to the temp folder.
# -FileGb       Total size of the test files, shared between the threads.
# -Threads      Requests in flight. More of them raises latency, as they queue for the disk.
# -BlockKb      Size of each request. Must be a multiple of 4.
#
# Every request goes to the disk rather than the file cache, and the figures it prints are its own,
# to compare with what HEALTH shows. The test files are removed afterwards, even if this is killed.

[CmdletBinding(PositionalBinding=$false)]
Param(
    [ValidateRange(1, 86400)][int] $Seconds = 120,
    [string] $Path = [System.IO.Path]::GetTempPath(),
    [ValidateRange(0.01, 1024)][double] $FileGb = 4,
    [ValidateRange(1, 1024)][int] $Threads = 64,
    [ValidateRange(4, 65536)][int] $BlockKb = 4096,
    [ValidateRange(0, 100)][int] $ReadPercent = 30
)

$ErrorActionPreference = "Stop"

$Alignment = 4096

if ($BlockKb % 4 -ne 0) {
    Write-Error "-BlockKb must be a multiple of 4."
    exit 1
}

$fullPath   = (Resolve-Path $Path).Path
$blockBytes = $BlockKb * 1KB
$blockCount = [int][math]::Max(1, [math]::Floor($FileGb * 1GB / $Threads / $blockBytes))
$fileBytes  = [long]$blockCount * $blockBytes
$totalBytes = $fileBytes * $Threads
$freeBytes  = (New-Object System.IO.DriveInfo([System.IO.Path]::GetPathRoot($fullPath))).AvailableFreeSpace

if ($freeBytes -lt $totalBytes * 2) {
    Write-Error "Not enough free space on $fullPath for $FileGb GB of test files."
    exit 1
}

# FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH. No enum name for the first, hence the raw value.
$unbuffered = [Enum]::ToObject([System.IO.FileOptions], 0xA0000000)

$worker = {
    param($state, $counters, [int] $id, [string] $file, $options, [int] $blockBytes, [int] $blockCount, [int] $readPercent, [int] $alignment)

    $ErrorActionPreference = "Stop"

    # Unbuffered requests need a buffer aligned to the sector size, which pinning and then starting
    # part way through it gives.
    $buffer = New-Object byte[] ($blockBytes + $alignment)
    $random = New-Object Random($id * 7919 + [Environment]::TickCount)
    $random.NextBytes($buffer)

    $pin    = [System.Runtime.InteropServices.GCHandle]::Alloc($buffer, "Pinned")
    $stream = [System.IO.FileStream]::new(
        $file,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]"ReadWrite, Delete",
        1,
        [System.IO.FileOptions]$options)

    try {
        $address = $pin.AddrOfPinnedObject().ToInt64()
        $start   = [int](($alignment - ($address % $alignment)) % $alignment)
        $slot    = $id * 2
        $tick    = [System.Diagnostics.Stopwatch]

        while (-not $state.Stop) {
            $stream.Position = [long]$random.Next($blockCount) * $blockBytes
            $began           = $tick::GetTimestamp()

            if ($random.Next(100) -lt $readPercent) {
                [void]$stream.Read($buffer, $start, $blockBytes)
            }
            else {
                $stream.Write($buffer, $start, $blockBytes)
            }

            $counters[$slot + 1] += $tick::GetTimestamp() - $began
            $counters[$slot]++
        }
    }
    finally {
        $stream.Dispose()
        $pin.Free()
    }
}

# One file per thread, as requests to a single file line up before they reach the disk, which then
# never sees a queue.
$files  = @(0..($Threads - 1) | ForEach-Object { Join-Path $fullPath "taskmon-stress-$PID-$_.tmp" })
$owners = New-Object 'System.Collections.Generic.List[System.IO.FileStream]'

$pool    = $null
$workers = @()

$state    = [hashtable]::Synchronized(@{ Stop = $false })
$counters = New-Object long[] ($Threads * 2)
$failure  = $null

try {
    Write-Host ("Writing {0:0.##} GB of test files to {1}" -f ($totalBytes / 1GB), $fullPath)

    $seed = New-Object byte[] 1MB
    (New-Object Random).NextBytes($seed)

    foreach ($file in $files) {
        # Held open with DeleteOnClose for the whole run, so Windows removes it however this ends.
        $owner = [System.IO.FileStream]::new(
            $file,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]"ReadWrite, Delete",
            1MB,
            [System.IO.FileOptions]::DeleteOnClose)

        $owners.Add($owner)

        for ($written = 0L; $written -lt $fileBytes; $written += $seed.Length) {
            $owner.Write($seed, 0, [int][math]::Min([long]$seed.Length, $fileBytes - $written))
        }

        $owner.Flush($true)
    }

    $pool = [runspacefactory]::CreateRunspacePool(1, $Threads)
    $pool.Open()

    $workers = @(0..($Threads - 1) | ForEach-Object {
        $shell = [powershell]::Create().AddScript($worker)

        foreach ($argument in @($state, $counters, $_, $files[$_], $unbuffered, $blockBytes, $blockCount, $ReadPercent, $Alignment)) {
            [void]$shell.AddArgument($argument)
        }

        $shell.RunspacePool = $pool
        [pscustomobject]@{ Shell = $shell; Handle = $shell.BeginInvoke() }
    })

    Write-Host "Running $Threads threads of $BlockKb KB requests, $ReadPercent% reads, for $Seconds seconds. Ctrl+C stops it early."

    $clock        = [System.Diagnostics.Stopwatch]::StartNew()
    $lastSeconds  = 0.0
    $lastRequests = 0L
    $lastTicks    = 0L

    while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
        Start-Sleep -Seconds 1

        if (@($workers | Where-Object { $_.Handle.IsCompleted }).Count -gt 0) {
            break
        }

        $elapsed = $clock.Elapsed.TotalSeconds

        if ($elapsed - $lastSeconds -ge 5) {
            $requests = 0L
            $ticks    = 0L

            for ($i = 0; $i -lt $counters.Length; $i += 2) {
                $requests += $counters[$i]
                $ticks    += $counters[$i + 1]
            }

            $newRequests = $requests - $lastRequests
            $latency     = if ($newRequests -gt 0) { ($ticks - $lastTicks) / $newRequests * 1000 / [System.Diagnostics.Stopwatch]::Frequency } else { 0 }

            Write-Host ("{0,6:0} requests/s  {1,8:0.0} MB/s  {2,7:0.0} ms average" -f
                ($newRequests / ($elapsed - $lastSeconds)),
                ($newRequests * $BlockKb / 1024 / ($elapsed - $lastSeconds)),
                $latency)

            $lastSeconds  = $elapsed
            $lastRequests = $requests
            $lastTicks    = $ticks
        }
    }
}
finally {
    $state.Stop = $true

    foreach ($entry in $workers) {
        try {
            $entry.Shell.EndInvoke($entry.Handle) | Out-Null
        }
        catch {
            if ($null -eq $failure) {
                $failure = $_.Exception.Message
            }
        }

        $entry.Shell.Dispose()
    }

    if ($null -ne $pool) {
        $pool.Dispose()
    }

    foreach ($owner in $owners) {
        $owner.Dispose()
    }

    Write-Host "Removed the test files."
}

if ($null -ne $failure) {
    Write-Error "A worker failed: $failure"
    exit 1
}
