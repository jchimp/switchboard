# ADR-0014: Renaming a live terminal applies everywhere now; title recovery settles later

**Status:** Accepted · 2026-09-05 · Qualifies invariant 4 (name == wt window name == title)

## Context
M2's rail offers inline rename and recolor. Recolor is easy — the color is ours on every
surface. Rename collides with invariant 4: the name is also the `wt -w` window name and the
initial title (ADR-0002), and Windows Terminal cannot rename a live window. Options:
forbid renaming while the window runs (user-hostile for long-lived sessions — the terminals
most worth renaming), keep a persistent alias table for recovery (a second source of
association truth that accumulates and can mis-bind), or rename now and accept a narrow
degraded path.

## Decision
- Rename applies immediately: the registry row (via the session's persist path), the strip
  segment, the rail card, the tracker binding (rekeyed in place — the window stays bound by
  HWND, so jumping, status, and color continue seamlessly), and the status engine's entry
  (state moves to the new name; Attention survives a rename, invariant 6).
- The wt window's own name/title are not touched — they can't be. Invariant 4 is therefore
  qualified: the triple holds *at launch*; after a live rename, the window name stays stale
  until the terminal itself is relaunched.
- The degraded path is narrow and self-healing: HWND-first restart recovery (ADR-0002
  amendment) re-binds by handle regardless of names or titles, so a renamed terminal
  survives a Switchboard restart. Only the double failure — Switchboard restarts *after a
  reboot* (stale handle) with the terminal's title also drifted — lands the window in the
  rail's Unbound list, one click from re-adoption.
- The edit popup (`RailEditPopup`) is the focusable escape hatch ADR-0012 reserved: it
  opens only on an explicit click, captures the previous foreground window first, and
  restores it on close. Recolor rides the same popup rather than a separate mouse-only
  flyout — one affordance, and the focus round-trip is invisible because it's restored.

## Consequences
- `wt -w <old-name>` from outside Switchboard would target the old window name until
  relaunch; Switchboard itself never does this after launch, so nothing in-product breaks.
- Validation (slug shape, uniqueness) reuses `TerminalDefinition.Validate` plus a
  session-level uniqueness check; the popup shows the error inline and stays open.
- A rename churns one registry write and one `RegistryChanged` (hotkeys rebind, strip
  rebuilds) — the same cost as any terminal edit.
