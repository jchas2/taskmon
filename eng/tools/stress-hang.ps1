# Opens a window and stops it responding, for trying the Apps row in the HEALTH view. See "Stress
# Testing" in README.md.
#
# Usage (from the repo root):
#   .\eng\tools\stress-hang.ps1 [-Seconds 60]
#
# Windows takes about 5 seconds to call a window hung, so the Apps row starts to count after that.
# Clicking on the window is harmless, and it closes itself when the time is up.

[CmdletBinding(PositionalBinding=$false)]
Param(
    [ValidateRange(1, 3600)][int] $Seconds = 60
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms

$form = New-Object System.Windows.Forms.Form
$form.Text = "taskmon stress-hang"
$form.Show()

[System.Windows.Forms.Application]::DoEvents()

Write-Host "This window has stopped reading its messages for $Seconds seconds."

# Spins rather than sleeps, as a sleep in PowerShell can still pump the window's messages.
$clock = [System.Diagnostics.Stopwatch]::StartNew()

while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
    [System.Threading.Thread]::SpinWait(1000000)
}

$form.Dispose()
Write-Host "Closed the window."
