# ADR-0024: The install script installs and updates the terminal host by default

**Status:** Accepted · 2026-11-23 · Amends ADR-0023 "The terminal host is opt-in"

## Context
ADR-0023 made the private terminal host (ADR-0021) opt-in through `install.ps1
-WithTerminalHost`, and skipped any folder that already had `wt.exe`. That left two gaps:

- **Install.** A user who leaves out the switch gets no host, and nothing says so. Switchboard
  then falls back to `wt.exe` on PATH and can bind any WT window. That is the problem ADR-0021
  exists to fix. On a machine with no installed WT, launches fail.
- **Upgrade.** Because an existing host is skipped, a new `$WtVersion` in a release never
  reaches users who already have a host. The only way to update is to delete the folder by
  hand, which also deletes the host's settings (profiles, the imported appearance).

The goal is one command to install and the same command to upgrade.

## Decision
- **The host is on by default.** `install.ps1` installs it when it is missing.
  `-NoTerminalHost` opts out; with it and no host present, the script warns that Switchboard
  will use the installed WT. `-WithTerminalHost` is removed. It never shipped in a release.
- **Every run brings the host to the pinned version.** After an install the script writes
  `.switchboard-wt-version` (the pinned version) into the host folder. A host whose marker
  matches is left alone and nothing is downloaded. A host with an older marker, or with no
  marker (installed by hand or by the ADR-0023 script), is replaced.
- **An update keeps `settings\`.** It holds the portable WT's `settings.json`, the
  appearance-import backup (ADR-0022) and WT's window state. The binaries are replaced as a
  whole, so files dropped from a newer WT do not linger.
- **The swap can be rolled back.** The old folder is renamed to `wt.old-<id>`, the new folder
  is built beside it, and `settings\` is copied across. On any failure the new folder is
  removed and the old one renamed back. The old folder is deleted only after success.
- **A running host is never touched.** If a `WindowsTerminal`, `OpenConsole`, `wt` or
  `elevate-shim` process runs from the host folder, the script warns and keeps the host at
  its version. Stopping those processes would kill the shells in Switchboard's terminals.
  This process check is the only guard: Windows allows the folder rename even while an exe in
  it runs, so the rename does not catch a running host by itself. Paths are compared in long
  form, because `%LOCALAPPDATA%` can hold an 8.3 short name.
- **A failed host step does not fail the install.** A download error, a hash mismatch or an
  unexpected zip layout is a warning. Switchboard itself is still installed, and any existing
  host is kept.

## Consequences
- First install needs to reach GitHub. Offline, the app installs without a host and the user
  runs the script again later.
- A user who keeps Switchboard's terminals open all the time never gets a host update until
  they close them once. The warning tells them each run.
- The host folder is always `%LOCALAPPDATA%\Switchboard\wt`. A user who points Settings →
  General at another folder still gets this one installed and updated; the app ignores it,
  because a folder the user set always wins (ADR-0023). Use `-NoTerminalHost` to avoid that.
- Anything a user puts in the host folder outside `settings\` is lost on the next update.
- Moving to a newer WT is still a two-line edit of `$WtVersion` and `$WtSha256`; existing
  installs pick it up on their next `install.ps1` run.
