# Keeps the CPU busy, for trying the CPU rules in the HEALTH view. See "Stress Testing" in README.md.
#
# Usage (from the repo root):
#   .\eng\tools\stress-cpu.ps1 [-Seconds 120] [-Percent 100] [-Threads 60]
#
# -Percent  How busy to keep each thread. 93 gives a Warning without reaching Critical.
# -Threads  Defaults to 3 per core at 100%, which also fills the run queue, and 1 per core otherwise.
#
# Threads run below normal priority, so the machine and taskmon keep working. Ctrl+C stops it early.

[CmdletBinding(PositionalBinding=$false)]
Param(
    [ValidateRange(1, 86400)][int] $Seconds = 120,
    [ValidateRange(1, 100)][int] $Percent = 100,
    [ValidateRange(0, 4096)][int] $Threads = 0
)

$ErrorActionPreference = "Stop"

if ($Threads -eq 0) {
    $Threads = if ($Percent -eq 100) { [Environment]::ProcessorCount * 3 } else { [Environment]::ProcessorCount }
}

# Hashes rather than looping over arithmetic in script: script allocates on every step, and with
# this many threads in one process garbage collection stalls them all and the CPU never fills up.
# Sleeps have a coarse timer on Windows, so a long period is what keeps -Percent accurate.
$worker = {
    param($state, [int] $percent)

    $ErrorActionPreference = "Stop"
    [System.Threading.Thread]::CurrentThread.Priority = "BelowNormal"

    $periodMs = 1000
    $busyMs   = $periodMs * $percent / 100
    $data     = New-Object byte[] 1MB
    $hash     = [System.Security.Cryptography.SHA256]::Create()
    $clock    = [System.Diagnostics.Stopwatch]::new()

    while (-not $state.Stop) {
        $clock.Restart()

        while ($clock.ElapsedMilliseconds -lt $busyMs) {
            [void]$hash.ComputeHash($data)
        }

        if ($busyMs -lt $periodMs) {
            Start-Sleep -Milliseconds ($periodMs - $busyMs)
        }
    }
}

$state = [hashtable]::Synchronized(@{ Stop = $false })
$pool  = [runspacefactory]::CreateRunspacePool(1, $Threads)
$pool.Open()

$workers = @(1..$Threads | ForEach-Object {
    $shell = [powershell]::Create().AddScript($worker).AddArgument($state).AddArgument($Percent)
    $shell.RunspacePool = $pool
    [pscustomobject]@{ Shell = $shell; Handle = $shell.BeginInvoke() }
})

Write-Host "Running $Threads threads at $Percent% for $Seconds seconds. Ctrl+C stops it early."

$clock = [System.Diagnostics.Stopwatch]::StartNew()

try {
    while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
        Start-Sleep -Seconds 1
    }
}
finally {
    $state.Stop = $true

    foreach ($entry in $workers) {
        $entry.Shell.EndInvoke($entry.Handle) | Out-Null
        $entry.Shell.Dispose()
    }

    $pool.Dispose()
    Write-Host "Stopped after $([int]$clock.Elapsed.TotalSeconds) seconds."
}
