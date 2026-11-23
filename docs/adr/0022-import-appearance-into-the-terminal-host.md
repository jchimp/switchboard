# ADR-0022: Import the installed Windows Terminal's appearance into the terminal host

**Status:** Accepted · 2026-11-22 · Extends ADR-0021; adds PRD R29

## Context
The terminal host (ADR-0021) is a portable Windows Terminal with its own `settings.json`. On
first run it generates the same dynamic profiles as the installed WT (PowerShell, Ubuntu,
Git Bash, …), and Switchboard launches by profile name, so terminals work without copying
anything. What the host lacks is the user's *look*: font, color scheme, cursor, opacity,
acrylic, window size. In daily use the dock's terminals then look different from every other
terminal the user has.

WT has no way to include another settings file, and a symlink is fragile: WT saves by writing
a new file, which replaces the link. Options considered:
1. **Tell the user to copy the file.** Copies everything, including settings that make the host
   behave like an everyday WT (startup, window restore). Easy to get wrong.
2. **Sync automatically on every start.** Always matches, but silently overwrites any look the
   user sets in the host, and writes another program's file without being asked.
3. **A one-shot import button** that copies only appearance.

## Decision
- **Option 3.** Settings → Terminals shows "Import appearance from installed Windows Terminal"
  when a host folder is set, the host has a `settings.json`, and an installed WT has one too.
  A hint appears above it when the host's `profiles.defaults` has no appearance keys and the
  installed WT's does: a fresh, unstyled host.
- **Only appearance moves** (`WtAppearanceImport` in Core):
  - `profiles.defaults` and each host profile matched by guid, then by name: the appearance
    keys (font, colors, cursor, opacity, acrylic, padding, background image, unfocused
    appearance, retro effect) become exactly the installed WT's. That includes removing keys the
    installed WT lacks, so importing twice gives the same file. An installed WT with no
    `profiles.defaults` counts as WT's default look, so the host's appearance defaults clear.
  - `schemes` and `themes` merge by name. The installed WT wins a clash, and host-only entries
    stay, so no host profile is left pointing at a missing scheme. Entries without a name are
    skipped: WT can't reference them, and they would be added again on every import.
  - `theme`, `initialCols`, `initialRows` and `useAcrylicInTabRow` follow the installed WT.
- **Never copied:** command lines, starting folders, the profile list itself, `defaultProfile`,
  key bindings, startup and window behaviour, and `tabColor`, because Switchboard passes each
  terminal's color with `--tabColor` (R9).
- **Overwrite, by the user's choice.** The button is an explicit request; a later import
  replaces the host's appearance again.
- **Safe write:** the first import copies the host file to `settings.json.switchboard.bak`, and
  later imports leave that backup alone, so it always holds the original, comments included.
  The merged file is written beside it and one move swaps it in, so WT's file watcher never
  reads half a file. WT hot-reloads, so open Switchboard terminals restyle at once.
- **Hostile input never throws.** Both files are hand-edited JSONC. Malformed text, a duplicate
  key (WT lets the last one win) or a top level that isn't an object makes the import report an
  error on the page. The hint check returns "not unstyled" and never stops Settings opening.
- Invariant 3 ("never hand-write JSON") is about Switchboard's registry. This is WT's file;
  Switchboard writes it only through this import, and only when the user presses the button.

## Consequences
- Comments in the host's `settings.json` are lost on import (JSON round trip). The file is WT's
  generated one, and the backup keeps the original. The rest of the file is rewritten with
  relaxed escaping, so quotes, `&&` and non-ASCII names stay readable.
- If WT itself rewrites the host file in the instant between the import's read and its write,
  WT's change is lost. The window is milliseconds and needs a WT save at that moment.
- A `backgroundImage` or shader path that points inside the Store package
  (`ms-appdata:///local/…`) may not resolve in the portable host.
- The key list follows WT's profile schema as of WT 1.25. A key WT adds later is not copied until
  the list is updated.
- Restyling the installed WT does not reach the host until the user imports again.
