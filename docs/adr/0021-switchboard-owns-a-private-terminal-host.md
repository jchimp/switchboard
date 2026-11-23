# ADR-0021: Switchboard runs its terminals in a private, portable Windows Terminal

**Status:** Accepted · 2026-11-22 · Amends ADR-0002, ADR-0009, PRD R5, R7, R22, R27; adds R28

## Context
Switchboard is a dock for a fixed set of about five terminals. Terminals the user opens from
Start or the taskbar are one-offs, separate from that set. Two things broke that separation:

1. **Binding.** Any WT window could be bound: by title at startup (fixed in ADR-0020), by
   adoption, or by import. Nothing distinguished "a window Switchboard launched" from "a
   window the user opened", because every WT window has the same class and the same process.
2. **Glomming.** All windows of the Store WT run in one `WindowsTerminal.exe`. With WT's
   `windowingBehavior` set to `useExisting`/`useAnyExisting`, a Start-menu launch opens as a
   tab inside the most recent WT window, and that can be a Switchboard terminal. Switchboard
   cannot see or prevent that from outside.

Options considered:
1. **Ask the user to set `windowingBehavior: useNew`.** Fixes glomming, but it is a global WT
   setting that changes how all the user's own terminals behave, and it does nothing for
   binding: the windows still share a process.
2. **Embed a terminal control (ConPTY + `Microsoft.Terminal.Wpf`) inside Switchboard.** Full
   ownership, but it replaces WT's profiles, tabs, settings and rendering. That is a rewrite,
   and PRD non-goals put Switchboard *above* WT, not instead of it.
3. **Run a second, portable WT just for Switchboard.** The unpackaged WT zip with a
   `.portable` marker file runs as its own `WindowsTerminal.exe` with its own settings folder.

A spike on 2026-11-22 with WT 1.25.3131.0 confirmed option 3: the portable copy at
`%LOCALAPPDATA%\Switchboard\wt` ran as PID 9972 from that folder while the Store WT ran as
PID 33584 from `WindowsApps`, and the portable copy keeps its settings in `wt\settings\settings.json`.

## Decision
- **New registry setting `terminalHost.folder`** (default null). It is stored as typed;
  environment variables are expanded at use. No version bump: old files load with null.
- **With a folder set:**
  - Launches run `<folder>\wt.exe` (`WtCommandBuilder.Build(def, executable)`).
  - A window is Switchboard's only if its process image is `<folder>\WindowsTerminal.exe`.
    `TerminalHost.Owns` checks it with `GetWindowThreadProcessId` → `OpenProcess`
    (`PROCESS_QUERY_LIMITED_INFORMATION`) → `QueryFullProcessImageName`, compared
    case-insensitively. A window whose path cannot be read is not ours.
  - The tracker uses `Owns` for launch binding (after the title matches a pending name, so a
    one-off Store window with the same title cannot win), `lastHwnd` recovery, manual binds,
    and the unbound scan that feeds the rail and Settings import.
  - The picker reads profiles from the host's own `settings.json` only, so it never offers a
    profile the host lacks.
- **With no folder set**, behaviour is as before ADR-0021: `wt.exe` on PATH, and any WT
  window can be bound (but still never by title at startup, ADR-0020).
- **The setting is on the Terminals settings page**, applied live (ADR-0018), with an inline
  error when the folder has no `wt.exe`. It affects new launches and new binds; windows
  already bound stay bound until they close.
- **No pid → path cache.** `Owns` runs on binds and on the rail's scan, never on the
  per-title-change path, so the process query is cheap and pid reuse is not a concern.
- **No installer.** The user downloads the WT zip and adds `.portable`; the README says how.
  *Amended by ADR-0023:* `install.ps1 -WithTerminalHost` does this for a pinned WT version,
  and with no folder set, `%LOCALAPPDATA%\Switchboard\wt` is used when it has `wt.exe`.

## Consequences
- The user's own WT windows are invisible to Switchboard: not bound, colored, hidden from the
  taskbar, or listed as unbound. ADR-0009's "unbound windows are never touched" becomes
  "foreign windows are never seen".
- A Store WT launch cannot open inside a Switchboard window, whatever its `windowingBehavior`.
- The portable WT does not auto-update. The user updates the zip by hand.
- Two WT installs means two settings files. Profiles, fonts and color schemes must be set up
  in the portable copy's `settings.json`.
- An elevated portable WT window cannot be queried by a non-elevated Switchboard, so it is
  treated as not ours and logged.
- Changing the folder while terminals run leaves them bound; after a restart, `lastHwnd`
  recovery drops any that are not in the new host.
