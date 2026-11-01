# Uses up memory, for trying the Memory rules in the HEALTH view. See "Stress Testing" in README.md.
#
# Usage (from the repo root), with exactly one of the first three options:
#   .\eng\tools\stress-memory.ps1 -AvailablePercent 8 [-Seconds 120] [-Churn]
#   .\eng\tools\stress-memory.ps1 -CommitPercent 92 [-Seconds 120]
#   .\eng\tools\stress-memory.ps1 -Gb 40 [-Seconds 120] [-Churn]
#
# -AvailablePercent  Fills memory until this much of it is left available, and keeps it there.
# -CommitPercent     Raises commit charge to this share of the commit limit without using any
#                    physical memory. Other programs can fail to start close to the limit.
# -Gb                Fills this many GB. Above the size of physical memory, the system has to page.
# -Churn             Keeps rewriting the memory while holding it. Together with a -Gb above the size
#                    of physical memory this causes hard faults, and the machine will be slow.
#
# The memory is released when the time is up or on Ctrl+C.

[CmdletBinding(PositionalBinding=$false)]
Param(
    [ValidateRange(3, 95)][int] $AvailablePercent = 0,
    [ValidateRange(1, 98)][int] $CommitPercent = 0,
    [ValidateRange(0.001, 4096)][double] $Gb = 0,
    [ValidateRange(1, 86400)][int] $Seconds = 120,
    [switch] $Churn
)

$ErrorActionPreference = "Stop"

$ChunkBytes = 256MB
$BlockBytes = 4MB

$modes = @($AvailablePercent, $CommitPercent, $Gb) | Where-Object { $_ -gt 0 }

if ($modes.Count -ne 1) {
    Write-Error "Pass exactly one of -AvailablePercent, -CommitPercent or -Gb."
    exit 1
}

if (-not [Environment]::Is64BitProcess) {
    Write-Error "This needs a 64-bit PowerShell."
    exit 1
}

function Get-MemoryState {
    $os = Get-CimInstance Win32_OperatingSystem

    [pscustomobject]@{
        AvailablePercent = 100.0 * $os.FreePhysicalMemory / $os.TotalVisibleMemorySize
        CommitPercent    = 100.0 * ($os.TotalVirtualMemorySize - $os.FreeVirtualMemory) / $os.TotalVirtualMemorySize
    }
}

function Show-MemoryState([string] $label) {
    $memory = Get-MemoryState
    Write-Host ("{0}: holding {1:0.0} GB, {2:0}% available, commit {3:0}%" -f
        $label, ($chunks.Count * $ChunkBytes / 1GB), $memory.AvailablePercent, $memory.CommitPercent)
}

function Test-TargetReached {
    if ($Gb -gt 0) {
        return ($chunks.Count * $ChunkBytes) -ge ($Gb * 1GB)
    }

    $memory = Get-MemoryState

    if ($AvailablePercent -gt 0) {
        return $memory.AvailablePercent -le $AvailablePercent
    }

    return $memory.CommitPercent -ge $CommitPercent
}

# Random data, as memory compression would absorb the load if the pages compressed well.
function Write-Chunk([IntPtr] $chunk) {
    for ($offset = 0; $offset -lt $ChunkBytes; $offset += $BlockBytes) {
        [System.Runtime.InteropServices.Marshal]::Copy($block, 0, [IntPtr]::Add($chunk, $offset), $BlockBytes)
    }
}

# Returns whether the system gave up the memory.
function Add-Chunk {
    try {
        $chunk = [System.Runtime.InteropServices.Marshal]::AllocHGlobal([int]$ChunkBytes)
    }
    catch [System.OutOfMemoryException] {
        Write-Warning "The system refused more memory, so holding what was allocated."
        return $false
    }

    $chunks.Add($chunk)

    # Commit is charged when memory is allocated, so leaving it unwritten uses no physical memory.
    if ($CommitPercent -eq 0) {
        Write-Chunk $chunk
    }

    return $true
}

$chunks = New-Object 'System.Collections.Generic.List[IntPtr]'
$block  = New-Object byte[] $BlockBytes
(New-Object Random).NextBytes($block)

try {
    Show-MemoryState "Starting"

    while (-not (Test-TargetReached) -and (Add-Chunk)) { }

    Show-MemoryState "Target reached"
    Write-Host "Holding for $Seconds seconds. Ctrl+C stops it early."

    # Windows trims memory left idle, and that counts as available again. So -AvailablePercent keeps
    # rewriting what it holds and adds to it when that isn't enough, up to the size of physical memory.
    $rewrite    = $Churn -or $AvailablePercent -gt 0
    $maxChunks  = (Get-CimInstance Win32_OperatingSystem).TotalVisibleMemorySize * 1KB / $ChunkBytes
    $clock      = [System.Diagnostics.Stopwatch]::StartNew()
    $lastReport = 0
    $next       = 0

    while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
        if ($rewrite -and $chunks.Count -gt 0) {
            Write-Chunk $chunks[$next]
            $next = ($next + 1) % $chunks.Count

            if ($next -eq 0 -and $AvailablePercent -gt 0 -and $chunks.Count -lt $maxChunks -and -not (Test-TargetReached)) {
                [void](Add-Chunk)
            }
        }
        else {
            Start-Sleep -Seconds 1
        }

        if ($clock.Elapsed.TotalSeconds - $lastReport -ge 10) {
            $lastReport = $clock.Elapsed.TotalSeconds
            Show-MemoryState "Holding"
        }
    }
}
finally {
    foreach ($chunk in $chunks) {
        [System.Runtime.InteropServices.Marshal]::FreeHGlobal($chunk)
    }

    Write-Host "Released the memory."
}
