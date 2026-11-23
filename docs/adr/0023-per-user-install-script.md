# ADR-0023: A per-user install script ships in the release zip

**Status:** Accepted · 2026-11-22 · Amends PRD non-functional "No installer required for v0", PRD R28, ADR-0021 "No installer" and its unset-folder default

## Context
A release is a zip of a single-file, self-contained `Switchboard.exe` plus a few native DLLs.
To use it, the user unzips it somewhere, makes their own shortcut, and repeats that on every
upgrade. Upgrading has a trap: a running `Switchboard.exe` is locked, so the copy fails
part-way. The private terminal host (ADR-0021) is a second manual step: download the WT zip,
extract it, add `.portable`.

`scripts/release.ps1` is a shared template that must stay byte-identical across repos. A repo
changes only `release.settings.psd1`. Its `ExtraPayload` list folds extra files into the zip,
and the template already suggests `install.ps1` there.

Options considered:
1. **A compiled installer (Inno Setup, WiX, MSIX).** A familiar `setup.exe`, but it needs a
   tool on the build machine and a second artifact, which the zip-mode template cannot produce
   without changing the template for every repo. MSIX also needs signing.
2. **`install.ps1` in the zip.** No new tools, no template change, readable by the user before
   they run it.

## Decision
- **`install.ps1` and `uninstall.ps1` ship in the release zip** through `ExtraPayload`.
  `release.ps1` is unchanged.
- **Per user, no admin.** Installs to `%LOCALAPPDATA%\Programs\Switchboard` (overridable
  with `-InstallDir`), adds a Start Menu shortcut, and writes an uninstall entry under
  `HKCU\...\Uninstall\Switchboard` so Settings > Apps can remove it.
- **Upgrade in place.** The script stops a `Switchboard.exe` running from the install folder,
  clears the folder, copies the new files, and starts it again if it was running. The registry
  is saved atomically, so stopping it cannot corrupt `switchboard.json`; the restart rebinds
  its terminals by `lastHwnd`. It refuses a non-empty folder that has no `Switchboard.exe`,
  because it clears the folder first.
- **It removes the downloaded-from-internet mark** from the installed files, so SmartScreen
  does not block the unsigned exe on every launch. The user has chosen to install it.
- **Start at login is opt-in** (`-StartAtLogin`). Without the switch an existing Startup
  shortcut is kept, so an upgrade keeps the earlier choice.
- **The terminal host is opt-in** (`-WithTerminalHost`). It downloads a pinned WT version,
  checks the SHA-256 against a constant in the script, extracts it to
  `%LOCALAPPDATA%\Switchboard\wt` and adds `.portable`. It skips a folder that already has
  `wt.exe`, so it never overwrites a host or its settings.
- **The installer never writes `switchboard.json`** (CLAUDE.md invariant 3). The app finds
  the host instead: when `terminalHost.folder` is unset and `%LOCALAPPDATA%\Switchboard\wt`
  has `wt.exe`, that folder is the host (`TerminalHostPath.Effective`, applied in
  `TerminalHost.Folder`). A folder the user set always wins, even one with no `wt.exe`, so a
  typo shows the Settings error instead of silently falling back. The check runs whenever
  the registry loads or changes, not on a timer.
- **The uninstaller keeps user data**: `%APPDATA%\Switchboard` and the terminal host. It
  removes a shortcut or the uninstall entry only if it points at its own install folder.
- **Windows PowerShell 5.1 is the target**, because every Windows machine has it. Started from
  a PowerShell 7 window, 5.1 inherits 7's module paths and loads 7's Utility module, which
  lacks `Get-FileHash`; both scripts drop those paths for their own process first.

## Consequences
- Installing is: unzip, then `powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1`.
  There is no double-click installer.
- The pinned WT version moves only when someone edits `$WtVersion` and `$WtSha256`.
- With the default host present, an unset folder no longer means "the installed WT". To go
  back to it, delete or rename `%LOCALAPPDATA%\Switchboard\wt`. A host installed while
  Switchboard runs is picked up on the next registry change or restart; terminals already
  bound stay bound (as for any host change, ADR-0021).
- Uninstalling while "Hide from taskbar" is on stops Switchboard without giving back the
  hidden taskbar buttons (ADR-0009). They return after Explorer restarts; the script says so.
- The publisher name in Settings > Apps is a constant in `install.ps1`.
