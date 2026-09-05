<#
.SYNOPSIS
    Manual end-to-end test for the Switchboard attention pulse.

.DESCRIPTION
    Run this INSIDE one of the terminals Switchboard is tracking. It sleeps so you
    have time to focus a different window, then sets this window's title to include
    an attention trigger word. The title change flows WinEvent hook -> WindowTracker
    -> TerminalSession -> StatusEngine, matches the default global Attention rule
    (\b(waiting|done|finished|error|failed|traceback)\b), and the strip segment for
    this terminal should start pulsing (and the auto-hidden strip should slide out).

    Attention is sticky (invariant 6): it clears only when you focus this window
    again. Restoring the title does NOT clear it, which is why -RestoreTitle is safe
    to use during the test.

.EXAMPLE
    .\tools\Test-Attention.ps1 -DelaySeconds 8
    # click over to another window within 8 seconds, then watch the strip

.EXAMPLE
    .\tools\Test-Attention.ps1 -Trigger waiting -RestoreTitle
#>
[CmdletBinding()]
param(
    [int]$DelaySeconds = 5,

    # Must be a word the default global Attention rule matches (StatusRule.Defaults).
    [ValidateSet('waiting', 'done', 'finished', 'error', 'failed', 'traceback')]
    [string]$Trigger = 'error',

    # Put the original title back after the pulse is confirmed. Recommended: restart
    # recovery re-associates windows by title (ADR-0002), so a leftover test title
    # would break recovery if Switchboard restarts before this terminal does.
    [switch]$RestoreTitle,

    [int]$RestoreAfterSeconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$original = $Host.UI.RawUI.WindowTitle

Write-Host "Focus a DIFFERENT window now. Firing '$Trigger' in $DelaySeconds seconds..."
Start-Sleep -Seconds $DelaySeconds

$Host.UI.RawUI.WindowTitle = "$original - $Trigger"
Write-Host "Title set to '$original - $Trigger'."
Write-Host "EXPECT: this terminal's strip segment pulses; auto-hidden strip slides out and stays out."
Write-Host "CLEAR:  focus this window (click it, or Ctrl+Alt+Space jumps to the attention terminal)."

if ($RestoreTitle) {
    Start-Sleep -Seconds $RestoreAfterSeconds
    $Host.UI.RawUI.WindowTitle = $original
    Write-Host "Title restored. Attention should STILL be pulsing until this window is focused."
}
