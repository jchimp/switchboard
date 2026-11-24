#Requires -Version 5.1
<#
.SYNOPSIS
    Removes a Switchboard install made by install.ps1.

.DESCRIPTION
    Settings > Apps > Switchboard runs this script. It removes the install
    folder (the folder this script is in), the Start Menu and Startup
    shortcuts, and the uninstall entry.

    It keeps your data: %APPDATA%\Switchboard (switchboard.json, your
    terminals) and %LOCALAPPDATA%\Switchboard\wt (the terminal host). Delete
    those by hand if you no longer want them.

.PARAMETER DryRun
    Print what would happen and change nothing.
#>
[CmdletBinding()]
param(
    [switch]$DryRun
)

# Started from a PowerShell 7 window, Windows PowerShell inherits 7's module
# paths and can load 7's modules. Drop those paths for this process.
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $env:PSModulePath = (($env:PSModulePath -split ';') | Where-Object {
            $_ -and $_ -notmatch '\\PowerShell\\|\\Microsoft\.PowerShell_'
        }) -join ';'
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$AppName = 'Switchboard'
$ExeName = 'Switchboard.exe'
$UninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Switchboard'

function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Note { param([string]$Message) Write-Host "    $Message" -ForegroundColor DarkGray }
function Write-Warn { param([string]$Message) Write-Host "  ! $Message" -ForegroundColor Yellow }

function Invoke-Change {
    # Every write goes through here, so -DryRun cannot miss one.
    param([string]$Description, [scriptblock]$Action)
    if ($DryRun) {
        Write-Note "would $Description"
    }
    else {
        Write-Note $Description
        & $Action
    }
}

function Get-FullPath {
    param([string]$Path)
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Get-ShortcutTarget {
    param([string]$Path)
    $shell = New-Object -ComObject WScript.Shell
    return $shell.CreateShortcut($Path).TargetPath
}

$installDir = Get-FullPath $PSScriptRoot
$exePath = Join-Path $installDir $ExeName

# This script deletes the folder it is in, so make sure that folder is a
# Switchboard install before anything else.
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "$ExeName is not next to uninstall.ps1, so $installDir does not look like a Switchboard install. Nothing was removed."
}

$modeText = ''
if ($DryRun) { $modeText = ' (dry run, nothing will change)' }
Write-Step "Uninstalling $AppName from $installDir$modeText"

$running = @(Get-Process -Name $AppName -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and ((Get-FullPath $_.Path) -ieq $exePath)
    })
if ($running.Count -gt 0) {
    Invoke-Change "stop the running $AppName" {
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }
    # A stopped Switchboard cannot give back taskbar buttons it hid (ADR-0009).
    Write-Warn 'If "Hide from taskbar" was on, its terminals get their taskbar buttons back after Explorer restarts or you sign out.'
}

# Remove a shortcut only if it points into this install, so a shortcut the
# user pointed somewhere else is left alone.
foreach ($folder in 'Programs', 'Startup') {
    $link = Join-Path ([Environment]::GetFolderPath($folder)) "$AppName.lnk"
    if (-not (Test-Path -LiteralPath $link)) { continue }
    $target = Get-ShortcutTarget -Path $link
    if ($target -and ((Get-FullPath $target) -ieq $exePath)) {
        Invoke-Change "remove $link" { Remove-Item -LiteralPath $link -Force }
    }
    else {
        Write-Note "$link points to $target, not this install, so it is kept."
    }
}

if (Test-Path -LiteralPath $UninstallKey) {
    # StrictMode throws on a missing property, and a hand-made key may lack it.
    $prop = (Get-ItemProperty -LiteralPath $UninstallKey).PSObject.Properties['InstallLocation']
    $location = $null
    if ($prop) { $location = $prop.Value }
    if ($location -and ((Get-FullPath $location) -ieq $installDir)) {
        Invoke-Change "remove $UninstallKey" { Remove-Item -LiteralPath $UninstallKey -Recurse -Force }
    }
    else {
        Write-Note "$UninstallKey belongs to an install at $location, so it is kept."
    }
}

Invoke-Change "remove $installDir" {
    # Windows will not delete a folder that is the current directory.
    Set-Location -LiteralPath ([System.IO.Path]::GetTempPath())
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

Write-Step 'Done'
if ($DryRun) {
    Write-Note 'Dry run: nothing was changed. Run again without -DryRun to uninstall.'
}
else {
    Write-Note "$AppName is uninstalled."
}
Write-Note "Kept: $(Join-Path $env:APPDATA 'Switchboard') (your terminals)"
$hostDir = Join-Path $env:LOCALAPPDATA 'Switchboard\wt'
if (Test-Path -LiteralPath $hostDir) {
    Write-Note "Kept: $hostDir (terminal host)"
}
