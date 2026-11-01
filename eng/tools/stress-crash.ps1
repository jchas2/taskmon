# Crashes a throwaway process, so a real report appears under Recent events in the HEALTH view. See
# "Stress Testing" in README.md.
#
# Usage (from the repo root):
#   .\eng\tools\stress-crash.ps1
#
# Nothing else is affected, but Windows records the crash in the Application event log, so it stays
# listed in HEALTH for the lookback period. HEALTH reads the logs once a minute.

$ErrorActionPreference = "Stop"

$process = Start-Process powershell.exe -PassThru -Wait -WindowStyle Hidden `
    -ArgumentList '-NoProfile', '-Command', "[Environment]::FailFast('taskmon stress-crash')"

Write-Host "Crashed a throwaway process, pid $($process.Id)."
