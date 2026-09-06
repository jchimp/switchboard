# ADR-0017: Ctrl+Alt+N opens the picker; profiles come from WT's settings.json

**Status:** Accepted · 2026-09-06 · Implements PRD R21/R22, ROADMAP M3 picker

## Context
Ctrl+Alt+N launched straight from the quick-launch defaults — good muscle memory, but the
only way to a different profile or directory was hand-editing the JSON first. R22 asks for
a small picker; R21 asks new terminals to start in the last-used directory. The picker
needs a profile list, and Switchboard had no idea what profiles Windows Terminal has.

## Decision
- **Ctrl+Alt+N opens the picker** — the third activatable window, on the same
  `ActivatableOverlayWindow` contract as the switcher (ADR-0016). Tab / Shift+Tab cycle
  the profile chips, Enter launches, Esc cancels and restores focus, Ctrl+K toggles
  "keep" (Space must stay typeable: directories contain spaces).
- **It opens fully seeded** — next free name, next unused hue, default profile
  preselected, "keep" from `persistByDefault`, directory from `lastDirectory` — so a
  bare Enter reproduces the old defaults launch one keystroke later. For the true
  zero-UI path, **`newTerminalQuiet` (default `Ctrl+Alt+Shift+N`)** still calls
  `QuickLaunchService.CreateAndLaunchAsync` directly. Both are additive settings; no
  version bump.
- **Profiles are read from Windows Terminal's own settings.json**, not a curated copy.
  A pure Core parser (`WtSettingsReader`, System.Text.Json with comments and trailing
  commas allowed) handles both `profiles` shapes and skips hidden entries;
  `WtProfileProvider` (App) probes the packaged, Preview, and unpackaged paths and
  re-reads on every open. Read-only, degrade-to-default: no file, or garbage, means the
  picker offers just the registry's `defaultProfile`. Chips launch by **name**
  (`-p <name>`), never guid — dynamic profiles can lack a stable one.
- **The picked directory is remembered** in `quickLaunch.lastDirectory` via
  `TerminalSession.RememberLastDirectoryAsync` — the `RememberHwnd` quiet-save path (no
  `RegistryChanged`, watcher muted). Both launch paths fall back to it; a remembered
  directory that no longer exists is dropped with a log line, never a failed launch.
  WSL directories are shell-native paths and skip the existence check.

## Consequences
- The picker never launches a shell wrapper: with no `Command`, `WtCommandBuilder` emits
  no `--` tail, so the picked profile's own command line runs (ADR-0004 untouched).
- A picked non-default profile with a directory still passes `-d` with a Windows path;
  WT maps it for WSL profiles itself.
- `lastDirectory` hand-edited in the JSON wins at the next hot-reload — correct, the
  registry is the state (invariant 3).
- WT settings shape drift is absorbed by the parser returning empty rather than
  throwing; the picker is always openable.
- R21's "registered and launched immediately" now describes the quiet chord; the primary
  chord costs one Enter.
