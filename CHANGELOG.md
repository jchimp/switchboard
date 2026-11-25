# Changelog

All notable changes to Switchboard are recorded here. Add entries under
`## Unreleased` as you work; `scripts/release.ps1 -Version <x.y.z> -Apply` turns
that section into the release section and its text becomes the GitHub release notes.

## Unreleased

### Changed

- **A new install starts with a settings file.** The first time Switchboard runs, it writes
  `%APPDATA%\Switchboard\switchboard.json` with every default and four ready terminals,
  `term-1` to `term-4` on jump keys 1 to 4, so the dock is usable straight away. When the
  private Windows Terminal copy is installed, Settings → General → Terminal host folder shows
  its path instead of an empty box.

### Removed

- **Import from running windows.** The bulk import button in Settings → Terminals is gone.
  Add terminals with "+ Add terminal" or a quick launch; to take over a stray window from the
  private terminal host, use "Adopt as…" in the rail's menu.

## 0.1.3 (2026-11-23)

### Added

- **Installer.** The release zip now has `install.ps1`. Run it from the unzipped folder to
  install Switchboard for your user only (no admin) with a Start Menu shortcut and an entry in
  Settings > Apps to uninstall it. It also sets up the private Windows Terminal copy for you.
  Running it again upgrades both in place; the private copy keeps its profiles and look, and
  is left alone while its terminals are open. `-StartAtLogin` starts Switchboard when you sign
  in, and `-NoTerminalHost` skips the private copy. Your terminals and settings are never
  touched.

### Changed

- **The terminal host is found on its own.** With Settings → General → Terminal host folder
  left empty, Switchboard now uses `%LOCALAPPDATA%\Switchboard\wt` if it has `wt.exe`,
  instead of the installed Windows Terminal. A folder you set still wins. To go back to the
  installed Windows Terminal, delete or rename that folder.

## 0.1.2 (2026-11-22)

### Changed

- **Settings has a General tab.** The terminal host folder and the appearance import moved
  there. The Terminals tab is now just the terminal list and its editor, with a draggable
  splitter between them and a wider editor. Settings remembers its size and the split.
- **Rail cards are easier to read.** The terminal name is much larger (18 pt). The line
  under it is gone: the shell name ("pwsh") was dropped, and the distro and folder now show
  when you hover over a card. The jump number sits at the card's right edge with the edit
  pencil to its left. The top rail is shorter to match: its default height is now 176 DIP
  (was 190); an existing setting is kept, so lower it with Settings → Edge strip →
  "Rail height on the top edge".

## 0.1.1 (2026-11-22)

### Changed

- **Terminal editor profile picker.** In Settings → Terminals, the Windows Terminal
  profile is picked from the profiles WT actually has (the terminal host's when set),
  instead of typed. Picking one sets the shell, and for WSL the distro, so the two can
  no longer disagree. A read-only panel shows what the profile runs and looks like
  (command line, starting folder, font, colors), with a button to open WT's
  settings.json. A name WT doesn't know is flagged, because WT would open its default
  profile instead.

## 0.1.0 (2026-11-22)

First release. Everything from milestones M0 to M5.

### Added

- **Registry.** `%APPDATA%\Switchboard\switchboard.json` holds the named terminals:
  profile, shell, working folder, startup command, color, jump key. Saves are atomic,
  with a backup.
- **Edge strip.** An 8 px strip on the top, left or right screen edge with one colored
  segment per terminal. The active one glows, one that wants attention pulses. It never
  takes focus. Optional auto-hide to a colored sliver; closed terminals show grey-tinted.
- **Colored frames.** Each terminal's color is painted onto its window border
  (Windows 11) and passed to Windows Terminal as the tab color.
- **Hotkeys.** `Ctrl+Alt+1…9` jump, `Ctrl+Alt+N` new terminal, `Ctrl+Alt+Space` jump to
  the terminal that is waiting on you. All can be rebound.
- **Status.** Working, idle and attention are inferred from window titles, taskbar
  flashes and live-thumbnail motion. No shell setup needed. Attention stays until you
  focus the terminal.
- **Rail.** Slides out from the strip with live thumbnails of each terminal. Rename and
  recolor from the rail. Optional app-bar mode and hide-from-taskbar for bound windows.
- **Overlay switcher** (`` Ctrl+` ``). Thumbnail grid with fuzzy filter, 1–9, arrows,
  Enter and Esc.
- **Quick-launch picker** (`Ctrl+Alt+N`). Tab cycles Windows Terminal profiles, remembers
  the last folder. `Ctrl+Alt+Shift+N` launches without the picker.
- **Settings window.** Terminals table and editor, edge strip, hotkeys and status rules.
  Changes apply live. Hotkey rows show when another application owns a chord. Bulk import
  from running windows.
- **Private terminal host** (#3, ADR-0021). Set `terminalHost.folder` to a portable
  Windows Terminal copy and Switchboard launches its terminals there. Only that copy's
  windows can be bound, adopted or imported, and Start-menu terminals can no longer open
  as tabs inside them.
- **"…" menu** (#4). Replaces the rail's foot: New terminal, Adopt window, Settings,
  Reapply colors, Reload registry, Quit. The strip and rail right-click menus share it.
  Menus now take focus while open, so Esc and click-away close them.
- **Import appearance** (ADR-0022). A Settings → Terminals button copies font, colors,
  cursor, opacity, acrylic and padding from the installed Windows Terminal into the
  private host. It never copies command lines or profiles.

### Changed

- **No adoption by title** (#3, ADR-0020). On start and on registry reload, Switchboard
  binds only the window handle it saved. It no longer takes over any terminal whose title
  starts with a registered name. After a reboot, running terminals show as unbound and
  need one click to adopt.
- On the top edge in Centered mode, strip segments now line up with the rail cards (#4).

### Fixed

- The rail could come out half a pixel short at fractional scaling and drop the last
  card (#4).
- Live previews scrolled past the rail's list edge drew over the "…" button (#4).
- Clicking "…" to close its menu opened it again (#4).
