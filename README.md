<img src="assets/icon/switchboard-256.png" alt="" width="96" align="left" hspace="12" />

</br>
</br>
</br>

# Switchboard

A Windows Terminal switchboard: name your terminals, give each one a color, see at a glance which ones are working or waiting on you, and jump to any of them from anywhere — without six entries in the taskbar.

<br clear="left" />

## What it does

- **Registry** — a `switchboard.json` of named terminals: profile, shell, working dir, startup command, color, jump key. That file is the whole product state.
- **Edge strip** — an 8 px strip on the top (or left/right) screen edge, one colored segment per terminal. Active one glows; one that wants attention pulses.
- **Colored frames** — the terminal's color is painted onto its Windows Terminal window border (Windows 11) and passed as the tab color, so you always know which window you're looking at.
- **Hotkeys** — `Ctrl+Alt+1…9` jumps, `Ctrl+Alt+N` opens a fresh terminal (auto-named, auto-colored, registered), `Ctrl+Alt+Space` jumps to whichever terminal is waiting on you.
- **Status** — inferred from window titles, taskbar flashes and (later) thumbnail motion. No shell configuration required.

Slide-out rail with live thumbnails and the overlay switcher are milestones 2–3; see [ROADMAP.md](ROADMAP.md).

## Requirements

- Windows 11 (Windows 10 runs, but window frames won't be colored)
- [Windows Terminal](https://aka.ms/terminal): either a private portable copy (recommended, see below) or the installed one on `PATH` as `wt.exe`
- .NET 8 SDK to build

## Install

Download `Switchboard-<version>.zip` from Releases, unzip it, and in the unzipped folder run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -WithTerminalHost
```

It installs for your user only (no admin) to `%LOCALAPPDATA%\Programs\Switchboard`, adds a
Start Menu shortcut, and adds Switchboard to Settings > Apps so you can uninstall it there.
Run it again with a newer zip to upgrade: it stops a running Switchboard, replaces the files
and starts it again.

| Switch | Does |
|---|---|
| `-WithTerminalHost` | Downloads a pinned portable Windows Terminal to `%LOCALAPPDATA%\Switchboard\wt`, checks its SHA-256 and marks it portable. Skipped if that folder already has `wt.exe` |
| `-StartAtLogin` | Starts Switchboard when you sign in |
| `-InstallDir <path>` | Installs somewhere else |
| `-DryRun` | Prints what it would do and changes nothing |

Uninstalling keeps `%APPDATA%\Switchboard` (your terminals) and the terminal host. See
ADR-0023 for the details. The exe also runs straight from the unzipped folder without installing.

## Private terminal host (recommended)

Switchboard can run its terminals in its own portable copy of Windows Terminal. That copy is a
separate process, so Switchboard never touches the terminals you open from Start or the
taskbar, and those can never open as a tab inside a Switchboard window (ADR-0021).

```powershell
$dst = "$env:LOCALAPPDATA\Switchboard\wt"
$tag = gh release view --repo microsoft/terminal --json tagName --jq .tagName   # e.g. v1.25.3131.0
$ver = $tag.TrimStart('v')
gh release download $tag --repo microsoft/terminal --clobber --pattern "Microsoft.WindowsTerminal_${ver}_x64.zip" --output "$env:TEMP\wt.zip"
Expand-Archive "$env:TEMP\wt.zip" "$env:TEMP\wtx" -Force
New-Item -ItemType Directory $dst -Force | Out-Null
Copy-Item "$env:TEMP\wtx\*\*" $dst -Recurse -Force
New-Item "$dst\.portable" -ItemType File -Force | Out-Null
```

`install.ps1 -WithTerminalHost` does the steps above for you. With **Settings → General →
Terminal host folder** left empty, Switchboard uses `%LOCALAPPDATA%\Switchboard\wt` whenever it
has `wt.exe`, so there is nothing to set. To keep the copy somewhere else, put that folder in
the setting (or `"terminalHost": { "folder": "..." }` in `switchboard.json`). To go back to the
installed Windows Terminal, clear the setting and delete or rename that folder. The portable copy keeps its
own settings in `wt\settings\settings.json`. It generates the same profiles as your installed
Windows Terminal (PowerShell, Ubuntu, …), so terminals work as they are. To give it the same
look, press **Import appearance from installed Windows Terminal** on the same settings page: it
copies fonts, colors, opacity and schemes, never command lines or profiles, and keeps the
original file as `settings.json.switchboard.bak` (ADR-0022). The portable copy does not auto-update:
repeat the steps above to update it.

## Build & run

```powershell
dotnet build
dotnet test
dotnet run --project src/Switchboard.App
```

On first run there is no registry. Copy `docs/switchboard.example.json` to `%APPDATA%\Switchboard\switchboard.json` and edit it, or just press `Ctrl+Alt+N` a few times.

## Layout

```
src/
  Switchboard.Core/      pure logic — registry model, status engine, wt command builder, hotkey chords. No Win32, no WPF.
  Switchboard.Windows/   Win32 interop — DWM frame color, WinEvent window tracker, global hotkeys, focus.
  Switchboard.App/       WPF — composition root, TerminalSession orchestrator, strip window, view models.
tests/
  Switchboard.Core.Tests/  xunit, runs cross-platform.
docs/
  PRD.md, ROADMAP.md, adr/   product, plan, and the decisions behind the shape of the code.
  design/                    the interactive mockups the UI is built from.
assets/
  icon/                      app icon — switchboard.ico is the exe icon, .svg is the source.
```

## How a registry row becomes a window

```
wt -w anvil new-tab -p Ubuntu --title anvil --tabColor #B07CF5 -- wsl.exe -d Ubuntu --cd ~/anvil -- bash -lic "claude --resume; exec bash -l"
```

1. `TerminalSession.LaunchAsync` tells the `WindowTracker` to expect a window titled `anvil`, then runs the command above with the terminal host's `wt.exe`.
2. The tracker's WinEvent hook sees a new `CASCADIA_HOSTING_WINDOW_CLASS` window whose title is `anvil`, checks that its process is the host's `WindowsTerminal.exe`, and binds the HWND to the name.
3. `FrameColorizer` sets `DWMWA_BORDER_COLOR` on that HWND. From here on the window is tracked by HWND, so the shell is free to change the title, and those title changes feed the `StatusEngine`.

Details and trade-offs are in `docs/adr/`.

## Status

Milestone 1 (strip + colored frames + launch/jump/new hotkeys) is scaffolded end to end. The code has not yet been built on a Windows machine with the .NET SDK — expect a short round of compiler fixes on the first `dotnet build`.
