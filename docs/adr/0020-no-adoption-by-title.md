# ADR-0020: Switchboard never adopts a window by its title

**Status:** Accepted · 2026-11-22 · Supersedes the title pass of ADR-0002, amends PRD R7 · Amended by ADR-0025 (Settings import removed)

## Context
Switchboard is meant to be a dock for a fixed set of about five terminals. Terminals the user
opens from Start or the taskbar are one-offs and are not Switchboard's business.

Restart recovery had two passes (ADR-0002 and its amendment): `RecoverByHwnd`, then
`RecoverByTitle`, which bound any visible WT window whose title equals a registered name or
starts with `name + " "`. A registry hot-reload ran the title pass again. In practice the title
pass caught the user's own windows: a one-off shell whose title happened to start with a
registered name was bound, colored, hidden from the taskbar (ADR-0009) and fed into status.
Since the HWND amendment, the title pass almost never recovers a real Switchboard window —
the log showed 0 title recoveries where the handle pass got them all.

## Decision
- **Delete `WindowTracker.RecoverByTitle`.** Startup recovery is by persisted `lastHwnd` only.
  The hot-reload path no longer scans the desktop at all.
- **Binding by title stays only for launches.** `ExpectWindow` still binds the first window
  whose title equals a *pending* name for 15 s after Switchboard itself runs `wt`. That window
  is one Switchboard just asked for.
- **Explicit adoption stays.** The rail's "Adopt as…" and Settings import (R27) are user
  actions on a specific window, so they are not takeover.

## Consequences
- After a reboot, or if a `lastHwnd` is stale, a running window is not re-bound automatically.
  It appears as unbound in the rail and can be adopted with one click. Before the HWND
  amendment that was already the common case.
- A terminal added by hand-editing the JSON while its window is open is not picked up by the
  reload; adopt it from the rail.
- This does not stop Store WT from opening a one-off tab *inside* a Switchboard window when
  WT's `windowingBehavior` is `useExisting`. That needs a separate WT process; see the next ADR.
