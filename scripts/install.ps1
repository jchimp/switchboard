#Requires -Version 5.1
<#
.SYNOPSIS
    Installs Switchboard for the current user. No admin rights needed.

.DESCRIPTION
    Run from the unzipped release folder:

        powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1

    Copies the release to %LOCALAPPDATA%\Programs\Switchboard, adds a Start
    Menu shortcut, and registers an uninstall entry in Settings > Apps for the
    current user only (HKCU). It also installs or updates the terminal host: a
    pinned portable Windows Terminal in %LOCALAPPDATA%\Switchboard\wt
    (ADR-0021). Running it again upgrades both in place.

    It never touches %APPDATA%\Switchboard\switchboard.json (your terminals) or
    the terminal host's settings folder (its profiles and appearance).

    See docs/adr/0023-per-user-install-script.md and
    docs/adr/0024-install-script-keeps-the-terminal-host-current.md for the
    reasons behind each choice.

.PARAMETER InstallDir
    Where Switchboard is installed. The folder must be empty, missing, or an
    earlier Switchboard install: everything in it is replaced.

.PARAMETER StartAtLogin
    Also add a shortcut to the Startup folder so Switchboard starts at login.
    Without this switch an existing Startup shortcut is left as it is, so an
    upgrade keeps the earlier choice. uninstall.ps1 removes it.

.PARAMETER NoTerminalHost
    Do not install or update the terminal host. Without a host, Switchboard
    uses the installed Windows Terminal and can bind any of its windows.

.PARAMETER DryRun
    Print what would happen and change nothing.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\Switchboard'),
    [switch]$StartAtLogin,
    [switch]$NoTerminalHost,
    [switch]$DryRun
)

# Started from a PowerShell 7 window, Windows PowerShell inherits 7's module
# paths and loads 7's Utility module, which has no Get-FileHash. Drop those
# paths for this process before anything loads a module.
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $env:PSModulePath = (($env:PSModulePath -split ';') | Where-Object {
            $_ -and $_ -notmatch '\\PowerShell\\|\\Microsoft\.PowerShell_'
        }) -join ';'
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Pinned so the download is checked against a known hash. To move to a newer
# WT, change both lines; the digest is on the release asset in the GitHub API:
#   gh api repos/microsoft/terminal/releases/tags/v<ver> --jq '.assets[].digest'
$WtVersion = '1.25.3131.0'
$WtSha256 = '273d6f1502eca1b5bd95c43f5d4d7ec72cc503df340102f8659717e5bead3437'
$WtUrl = "https://github.com/microsoft/terminal/releases/download/v$WtVersion/Microsoft.WindowsTerminal_${WtVersion}_x64.zip"

$AppName = 'Switchboard'
$ExeName = 'Switchboard.exe'
$UninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Switchboard'
$TerminalHostDir = Join-Path $env:LOCALAPPDATA 'Switchboard\wt'
# Written by this script after a host install. The only record of which WT
# version is in the folder.
$WtMarkerName = '.switchboard-wt-version'
# What the host keeps across updates: WT's portable settings (profiles, the
# imported appearance and its .bak, window state).
$WtSettingsName = 'settings'

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

function New-Shortcut {
    param([string]$Path, [string]$Target, [string]$WorkingDir)
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($Path)
    $link.TargetPath = $Target
    $link.WorkingDirectory = $WorkingDir
    $link.IconLocation = "$Target,0"
    $link.Description = $AppName
    $link.Save()
}

function Get-RunningInstances {
    # Only instances started from the install folder. A dev build running from
    # elsewhere is not ours to stop.
    param([string]$ExePath)
    return @(Get-Process -Name $AppName -ErrorAction SilentlyContinue | Where-Object {
            $_.Path -and ((Get-FullPath $_.Path) -ieq $ExePath)
        })
}

function Get-LongPath {
    # Expands 8.3 short names (JOHNSM~1) segment by segment. Each existing
    # segment is replaced by its name on disk; the rest is kept as given.
    param([string]$Path)
    $full = Get-FullPath $Path
    $root = [System.IO.Path]::GetPathRoot($full)
    $current = $root
    foreach ($segment in ($full.Substring($root.Length) -split '\\' | Where-Object { $_ })) {
        $match = $null
        if (Test-Path -LiteralPath $current -PathType Container) {
            $match = Get-ChildItem -LiteralPath $current -Force -Filter $segment -ErrorAction SilentlyContinue |
                Select-Object -First 1
        }
        if ($match) { $segment = $match.Name }
        $current = Join-Path $current $segment
    }
    return $current.TrimEnd('\')
}

function Get-InstalledWtVersion {
    # A host without the marker predates it or was set up by hand. Its version
    # is unknown, so it is refreshed once.
    $marker = Join-Path $TerminalHostDir $WtMarkerName
    if (-not (Test-Path -LiteralPath $marker)) { return $null }
    return ([string](Get-Content -LiteralPath $marker -Raw)).Trim()
}

function Get-HostProcesses {
    # A running host locks its files, and stopping it would kill the shells in
    # its windows. So the script finds these and leaves the host alone.
    # Long paths on both sides: a process reports its long path, but
    # %LOCALAPPDATA% may hold an 8.3 short name (JOHNSM~1).
    $prefix = "$(Get-LongPath $TerminalHostDir)\"
    $names = 'WindowsTerminal', 'OpenConsole', 'wt', 'elevate-shim'
    return @(Get-Process -Name $names -ErrorAction SilentlyContinue | Where-Object {
            $_.Path -and (Get-LongPath $_.Path).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
        })
}

function Install-HostFiles {
    # Swap in the new binaries and carry the settings folder over. The old host
    # is renamed aside first and put back if anything fails, so a failure never
    # leaves a half-replaced host.
    param([string]$NewFiles)

    $hostName = Split-Path -Leaf $TerminalHostDir
    $old = $null
    if (Test-Path -LiteralPath $TerminalHostDir) {
        $old = "$TerminalHostDir.old-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        # Windows allows this rename even while an exe in the folder runs, so
        # Get-HostProcesses is the only guard against a running host.
        Rename-Item -LiteralPath $TerminalHostDir -NewName (Split-Path -Leaf $old)
    }
    try {
        New-Item -ItemType Directory -Path $TerminalHostDir | Out-Null
        Copy-Item -Path (Join-Path $NewFiles '*') -Destination $TerminalHostDir -Recurse -Force
        if ($old) {
            $oldSettings = Join-Path $old $WtSettingsName
            if (Test-Path -LiteralPath $oldSettings) {
                Copy-Item -LiteralPath $oldSettings -Destination $TerminalHostDir -Recurse -Force
                Write-Note "kept $WtSettingsName\ (profiles and appearance)"
            }
        }
        # Without this marker WT shares the installed WT's settings (ADR-0021).
        New-Item -ItemType File -Path (Join-Path $TerminalHostDir '.portable') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $TerminalHostDir $WtMarkerName) -Value $WtVersion -Encoding ASCII
    }
    catch {
        $failure = $_
        Remove-Item -LiteralPath $TerminalHostDir -Recurse -Force -ErrorAction SilentlyContinue
        if ($old) {
            try {
                Rename-Item -LiteralPath $old -NewName $hostName
            }
            catch {
                throw "$($failure.Exception.Message) The previous host could not be put back; it is in $old. Rename it to $hostName."
            }
        }
        throw $failure
    }

    if ($old) {
        Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $old) {
            Write-Warn "Could not remove the previous host in $old. Delete it by hand."
        }
    }
}

function Install-TerminalHost {
    $hasHost = Test-Path -LiteralPath (Join-Path $TerminalHostDir 'wt.exe')
    $installed = Get-InstalledWtVersion
    if ($hasHost -and $installed -eq $WtVersion) {
        Write-Note "$TerminalHostDir already has Windows Terminal $WtVersion."
        return
    }

    $from = 'an unknown version'
    if ($installed) { $from = $installed }
    if ($hasHost) {
        $busy = @(Get-HostProcesses)
        if ($busy.Count -gt 0) {
            Write-Warn "Switchboard's terminals are open, so the host stays at $from."
            Write-Warn "Close them and run install.ps1 again to update it to $WtVersion."
            return
        }
        Write-Note "updating the host from $from to $WtVersion"
    }

    if ($DryRun) {
        Write-Note "would download $WtUrl"
        Write-Note "would check its SHA-256 is $WtSha256"
        if ($hasHost) {
            Write-Note "would replace the files in $TerminalHostDir and keep its $WtSettingsName\ folder"
        }
        else {
            Write-Note "would extract it to $TerminalHostDir and add a .portable marker"
        }
        return
    }

    $temp = Join-Path ([System.IO.Path]::GetTempPath()) "switchboard-wt-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $temp | Out-Null
    try {
        $zip = Join-Path $temp 'wt.zip'
        Write-Note "downloading Windows Terminal $WtVersion"
        # Windows PowerShell 5.1 may default to TLS 1.0, which GitHub refuses.
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        # The progress bar makes Invoke-WebRequest many times slower on 5.1.
        $oldProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        try {
            Invoke-WebRequest -Uri $WtUrl -OutFile $zip -UseBasicParsing
        }
        finally {
            $ProgressPreference = $oldProgress
        }

        $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $WtSha256) {
            throw "The Windows Terminal download has SHA-256 $actual, expected $WtSha256."
        }
        Write-Note 'SHA-256 matches'

        $extract = Join-Path $temp 'x'
        Expand-Archive -LiteralPath $zip -DestinationPath $extract
        # The zip holds one top folder (terminal-<version>); its contents are the host.
        $top = @(Get-ChildItem -LiteralPath $extract -Directory)
        if ($top.Count -ne 1 -or -not (Test-Path -LiteralPath (Join-Path $top[0].FullName 'wt.exe'))) {
            throw 'The Windows Terminal zip does not have the expected layout (one folder holding wt.exe).'
        }

        Install-HostFiles -NewFiles $top[0].FullName
        Write-Note "installed Windows Terminal $WtVersion to $TerminalHostDir"
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --- checks ----------------------------------------------------------------

$sourceDir = Get-FullPath $PSScriptRoot
$InstallDir = Get-FullPath $InstallDir
$sourceExe = Join-Path $sourceDir $ExeName
$targetExe = Join-Path $InstallDir $ExeName

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "$ExeName is not next to install.ps1. Run install.ps1 from the unzipped release folder."
}
if ($sourceDir -ieq $InstallDir) {
    throw "install.ps1 is running from the install folder itself ($InstallDir). Run it from the unzipped release folder."
}
# The install folder is wiped before the copy, so refuse anything that is not
# clearly ours. An empty or missing folder is fine.
if ((Test-Path -LiteralPath $InstallDir) -and
    (@(Get-ChildItem -LiteralPath $InstallDir -Force).Count -gt 0) -and
    -not (Test-Path -LiteralPath $targetExe)) {
    throw "$InstallDir has files but no $ExeName, so it is not a Switchboard install. Pick an empty folder with -InstallDir."
}

$version = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($sourceExe).ProductVersion -split '\+')[0]
$modeText = ''
if ($DryRun) { $modeText = ' (dry run, nothing will change)' }
Write-Step "Installing $AppName $version to $InstallDir$modeText"

# --- stop a running copy ---------------------------------------------------

# @() again here: a function's empty array comes back as $null on 5.1.
$running = @(Get-RunningInstances -ExePath $targetExe)
$wasRunning = $running.Count -gt 0
if ($wasRunning) {
    # A running exe is locked and the copy would fail. The registry is saved
    # atomically, so stopping it cannot leave a half-written switchboard.json.
    # It is started again at the end, which rebinds its terminals.
    Invoke-Change "stop the running $AppName (it is started again at the end)" {
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }
}
$others = @(Get-Process -Name $AppName -ErrorAction SilentlyContinue | Where-Object {
        -not $_.Path -or ((Get-FullPath $_.Path) -ine $targetExe)
    })
if ($others.Count -gt 0) {
    Write-Warn "$AppName is also running from somewhere else. Only one copy runs at a time, so quit that one before starting the installed one."
}

# --- copy ------------------------------------------------------------------

Write-Step 'Copying files'
$payload = @(Get-ChildItem -LiteralPath $sourceDir -Force | Where-Object { $_.Name -ne 'install.ps1' })
if (Test-Path -LiteralPath $InstallDir) {
    # Clear first so files dropped from a newer release do not linger.
    Invoke-Change "remove the previous files in $InstallDir" {
        Get-ChildItem -LiteralPath $InstallDir -Force | Remove-Item -Recurse -Force
    }
}
Invoke-Change "copy $($payload.Count) item(s) to $InstallDir" {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    $payload | Copy-Item -Destination $InstallDir -Recurse -Force
    # A zip downloaded from the internet marks its files as such, and SmartScreen
    # then blocks the unsigned exe on every launch. The user has chosen to
    # install it, so drop the mark on the installed copy only.
    Get-ChildItem -LiteralPath $InstallDir -Recurse -File | Unblock-File
}

# --- shortcuts -------------------------------------------------------------

Write-Step 'Shortcuts'
$startMenuLink = Join-Path ([Environment]::GetFolderPath('Programs')) "$AppName.lnk"
Invoke-Change "create the Start Menu shortcut $startMenuLink" {
    New-Shortcut -Path $startMenuLink -Target $targetExe -WorkingDir $InstallDir
}
$startupLink = Join-Path ([Environment]::GetFolderPath('Startup')) "$AppName.lnk"
if ($StartAtLogin) {
    Invoke-Change "create the Startup shortcut $startupLink" {
        New-Shortcut -Path $startupLink -Target $targetExe -WorkingDir $InstallDir
    }
}
elseif (Test-Path -LiteralPath $startupLink) {
    Write-Note 'Startup shortcut already exists and is kept.'
}

# --- uninstall entry -------------------------------------------------------

Write-Step 'Uninstall entry (Settings > Apps)'
$uninstallScript = Join-Path $InstallDir 'uninstall.ps1'
$powershellExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
Invoke-Change "write $UninstallKey" {
    $sizeKb = [int]((Get-ChildItem -LiteralPath $InstallDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1KB)
    New-Item -Path $UninstallKey -Force | Out-Null
    $values = @{
        DisplayName     = $AppName
        DisplayVersion  = $version
        Publisher       = 'Jeremy Champion'
        InstallLocation = $InstallDir
        DisplayIcon     = "$targetExe,0"
        UninstallString = "`"$powershellExe`" -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`""
    }
    foreach ($name in $values.Keys) {
        New-ItemProperty -Path $UninstallKey -Name $name -Value $values[$name] -PropertyType String -Force | Out-Null
    }
    foreach ($name in 'NoModify', 'NoRepair') {
        New-ItemProperty -Path $UninstallKey -Name $name -Value 1 -PropertyType DWord -Force | Out-Null
    }
    New-ItemProperty -Path $UninstallKey -Name 'EstimatedSize' -Value $sizeKb -PropertyType DWord -Force | Out-Null
}

# --- terminal host ---------------------------------------------------------

$hostReady = Test-Path -LiteralPath (Join-Path $TerminalHostDir 'wt.exe')
if (-not $NoTerminalHost) {
    Write-Step "Terminal host (portable Windows Terminal $WtVersion)"
    try {
        Install-TerminalHost
    }
    catch {
        # The app works without a host, so a failed download (offline, GitHub
        # down, a bad hash) must not fail the whole install.
        Write-Warn "The terminal host was not installed or updated: $($_.Exception.Message)"
        if ($hostReady) {
            Write-Warn "The existing host in $TerminalHostDir is kept. Run install.ps1 again to retry."
        }
        else {
            Write-Warn 'Run install.ps1 again to retry. Until then Switchboard uses the installed Windows Terminal.'
        }
    }
    $hostReady = Test-Path -LiteralPath (Join-Path $TerminalHostDir 'wt.exe')
}

# --- restart ---------------------------------------------------------------

if ($wasRunning) {
    Invoke-Change "start $AppName again" {
        Start-Process -FilePath $targetExe -WorkingDirectory $InstallDir
    }
}

Write-Step 'Done'
if ($DryRun) {
    Write-Note 'Dry run: nothing was changed. Run again without -DryRun to install.'
}
else {
    Write-Note "$AppName $version is installed. Start it from the Start Menu."
}
if ($hostReady) {
    # The registry is the app's to write (CLAUDE.md invariant 3). The app uses
    # this folder by itself when terminalHost.folder is unset (ADR-0023).
    Write-Note "Switchboard uses $TerminalHostDir as its terminal host unless Settings > General names another folder."
}
elseif ($NoTerminalHost) {
    # A failed install already warned in its catch block above.
    Write-Warn "No terminal host in $TerminalHostDir. Switchboard uses the installed Windows Terminal and can bind any of its windows."
    Write-Warn 'Run install.ps1 without -NoTerminalHost to add the host.'
}
Write-Note 'To uninstall: Settings > Apps > Switchboard, or run uninstall.ps1 in the install folder.'
