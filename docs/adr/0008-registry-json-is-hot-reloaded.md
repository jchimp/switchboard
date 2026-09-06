# ADR-0008: switchboard.json is hot-reloaded

**Status:** Accepted · 2026-09-04

## Context
Until the M4 settings UI, editing `switchboard.json` by hand is the sanctioned way to configure Switchboard — but every edit required an app restart, because `RegistryChanged` only fired from in-process writes. "The registry is the state" loses its force if the file on disk and the running app can quietly disagree.

## Decision
- A `RegistryWatcher` (Core, `FileSystemWatcher` filtered to the registry file name so `.tmp`/`.bak` writes never match) raises a debounced `Changed` event; `TerminalSession` marshals it to the UI thread and reloads.
- **Self-writes are muted by a time window**, not content comparison: every `SaveAsync` is preceded by a 2 s mute that covers the store's tmp-write → bak-copy → rename burst. The debounce/mute decision logic is a pure, tested class (`WatchGate`).
- Reload is **best-effort and never destructive**: a hand-edit that fails to parse logs a warning and keeps the current in-memory registry (user input is hostile — invariant 9 in spirit).
- On successful reload: global status rules are replaced, removed terminals are unbound (their windows become unbound WT windows), added names are recovered by title, colors are re-applied, and `RegistryChanged` fires so the strip rebuilds and re-docks. Status entries for surviving names are untouched — Attention stays sticky through a reload.

## Consequences
- Edits to colors, edges, thickness, monitor, terminals, and status rules apply live, usually within a second of saving.
- Deleting the file empties the strip and unbinds everything — correct by definition, since the registry is the state.
- The mute window means a hand-edit made within ~2 s of an in-app write can be missed; the next save or edit picks it up.
- **2026-09-05:** the file was renamed `registry.json` → `switchboard.json`. The store reads the old name once when the new one is absent (`JsonRegistryStore.LegacyDefaultPath`) and `TerminalSession.InitializeAsync` immediately writes the new name, so the watcher — which filters on the current file name — is never left watching a file that does not exist. The old file is left in place, untouched, as a de facto backup.
