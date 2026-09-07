# ADR-0015: Cycle-recent walks a frozen MRU snapshot

**Status:** Accepted · 2026-09-06 · Implements ROADMAP M3 "cycle-recent with MRU list"

## Context
`Ctrl+Shift+`` was a strict A↔B toggle: `TerminalSession` kept a single
`_previousForegroundName` slot, so the third-most-recent terminal was unreachable without
the mouse or a jump key. M3 replaces the slot with a real most-recently-used list, which
the overlay switcher also reads for its preselection. The naive version — activate
`Order[1]` on every tap — self-defeats: the activation reorders the list, so two quick
taps bounce between the same two terminals forever.

## Decision
- A pure `MruList` lives in Core (`Focus/MruList.cs`): `Touch`/`Forget`/`Rename`/`Prune`,
  most-recent first. `TerminalSession` feeds it from foreground events and follows
  rename (position preserved), remove, window-close, and registry-reload exactly as the
  old fields did. It is runtime-only — never persisted; a restart starts with an empty
  history, like Alt-Tab.
- **One tap = previous terminal. Taps within 1.5 s walk deeper into a snapshot frozen at
  the first tap.** The settle timer resets per tap; when it fires, only the terminal the
  walk landed on is touched. While a walk is live, the walk's own foreground events don't
  touch the list; any *other* foreground change (a click, a jump key) aborts the session
  uncommitted.
- The walk skips entries that are unregistered or have no live window, wrapping at the
  end: cycling must never stall behind a 15 s launch-and-bind. Relaunching a closed
  terminal stays the job of jump keys and the switcher.
- Window close forgets the entry — recency is a live-window notion; a relaunch re-earns
  its place.

## Consequences
- From a non-terminal window (browser), one tap returns to the most recent terminal —
  the snapshot's front entry is only skipped when it *is* the current foreground.
- A walk that wraps can land back on the starting terminal; that is Alt-Tab behavior,
  not a bug.
- The 1.5 s settle means a deliberate slow second tap (> 1.5 s) starts a new session
  from the *committed* order — tap, wait, tap returns you to where you came from.
- The switcher's "preselect most recent" (M3 Slice 2) reads the same list, so the two
  features can never disagree about what "recent" means.
