# ADR-0025: A first run writes its defaults and starter terminals, and bulk import is removed

**Status:** Accepted · 2026-11-24 · Amends ADR-0020 "Explicit adoption stays", ADR-0023 "The installer never writes `switchboard.json`" (unset-folder default), PRD R26, R27, R28

## Context
Switchboard began as a tool that took over Windows Terminal windows the user already had open.
Since ADR-0020 (no adoption by title) and ADR-0021 (a private terminal host) it is a dock and
switcher for terminals it launches itself. Two leftovers from the old model show on a new
install:

- **No registry file.** With no `switchboard.json`, `JsonRegistryStore.LoadAsync` returns
  `RegistryFile.Empty` in memory and nothing is written until the first edit. A new user has no
  file to read or hand-edit, and the registry watcher has nothing to watch.
- **An empty host folder.** `install.ps1` puts the host in `%LOCALAPPDATA%\Switchboard\wt` but
  must not write the registry (invariant 3), so `terminalHost.folder` stays unset. The app uses
  the folder anyway (ADR-0023 fallback), but Settings → General shows an empty box, which reads
  as "not configured".
- **Bulk import (R27)** lists every unbound host window for registration in one go. A host
  window is one Switchboard launched, so after a restart the realistic case is one or two
  windows, which the rail's one-click "Adopt as…" already covers.

## Decision
- **A first run writes the full default registry.** `IRegistryStore.FoundNoFile` is true when
  the load found neither the current nor the legacy file. `TerminalSession.InitializeAsync`
  then saves `RegistryFile.FirstRun` at once through `SaveAsync`, so the file holds every
  default.
- **A first run has four starter terminals**, `term-1` to `term-4`. Names use the
  quick-launch prefix so the next quick launch is `term-5`, not a second numbering. Each has
  the quick-launch profile and shell, its own palette hue, and jump key 1 to 4. They are
  registered, not launched; the rail shows them as closed until they are opened. A new user
  sees how the dock works without adding a terminal first.
- **That first save names the host folder** as `%LOCALAPPDATA%\Switchboard\wt` (unexpanded)
  when that folder has `wt.exe`. Otherwise the folder stays unset and the ADR-0023 fallback
  applies as before. Existing registries are not changed.
- **Bulk import is removed**: the Settings button, `ImportWindowsDialog`, `ImportViewModel`,
  `TerminalSession.ImportAsync` and `NameSlug`. PRD R27 is withdrawn.
- **The rail's "Adopt as…" stays** for one unbound host window at a time (ADR-0020).

## Consequences
- After a first run, removing the host folder makes launches fail with the Settings error
  instead of falling back to the installed WT. That is the ADR-0021 intent; clearing the box
  restores the fallback.
- A user who runs Switchboard before `install.ps1` finished the host gets an unset folder; the
  fallback still finds the host later.
- Several stray host windows after a restart are adopted one by one from the rail.
- A user who wants fewer terminals deletes the extra ones; nothing brings them back, since
  the starters are written only when there is no file at all.
