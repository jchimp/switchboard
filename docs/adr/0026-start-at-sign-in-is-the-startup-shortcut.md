# ADR-0026: Start at sign-in is the Startup-folder shortcut, not a registry setting

**Status:** Accepted · 2026-11-24 · Amends ADR-0018 (a General control that is not in `switchboard.json`), PRD R26

## Context
`install.ps1 -StartAtLogin` (ADR-0023) puts `Switchboard.lnk` in the user's Startup folder,
and `uninstall.ps1` removes it when it points into the install. After the install, the only
way to change the choice is to run the script again or to edit the Startup folder by hand.
Settings → General should have a checkbox for it.

Invariant 3 says the registry is the state. But this choice already lives outside it, in a
file Windows reads at sign-in, and that file can change without Switchboard: by the install
script, the uninstall script, or the user in Explorer. Windows also has its own on/off switch
for Startup-folder entries (Settings > Apps > Startup, Task Manager), kept under
`HKCU\...\Explorer\StartupApproved\StartupFolder`.

Options:
1. **A `startAtSignIn` field in `switchboard.json`** that the app applies to the shortcut. Two
   copies of one fact; they disagree as soon as a script or the user changes the shortcut.
2. **An `HKCU\...\Run` value.** A second mechanism beside the script's shortcut: a user who
   installs with `-StartAtLogin` and ticks the box gets two entries and two launches (the
   second stopped only by the single-instance guard).
3. **The shortcut is the state.** The checkbox reads the Startup folder and writes or deletes
   the same file the scripts use.

## Decision
- **Option 3.** `Switchboard.Windows.StartupShortcut` reads and writes
  `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Switchboard.lnk`, the path and name
  `install.ps1` uses, through `IShellLinkW`/`IPersistFile` (declared in `NativeMethods.cs`).
- **Nothing goes in `switchboard.json`**, so the settings queue (ADR-0018) is not used. The box
  applies at once, like the other General controls, and the page reads the file again each
  time Settings is shown.
- **On** writes the shortcut for the running exe, with its folder as the working directory.
  **Off** deletes the shortcut, whatever it points at: the user asked for no start at sign-in.
- **The page warns, and does not fix,** in two cases: the shortcut starts a different exe (a
  dev build, a moved install), and Windows' Startup apps switch is off. The second is the user's
  choice in Windows, so Switchboard reads that key but never writes it.

## Consequences
- The scripts, Explorer and the checkbox always agree, because they share one file.
- Turning the box on from a `bin\Debug` build points the shortcut at that build. The warning
  shows the target, and turning the box off and on again from the installed copy repoints it.
- When Windows' switch is off, the box shows on but Switchboard does not start. The warning
  says where to turn it back on.
- Reading the on/off byte relies on Windows' undocumented `StartupApproved` format (an odd
  first byte means off). If that changes, only the warning is wrong; the shortcut still works.
