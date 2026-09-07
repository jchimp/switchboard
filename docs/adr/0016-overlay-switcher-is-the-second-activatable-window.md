# ADR-0016: The overlay switcher is the second activatable window

**Status:** Accepted · 2026-09-06 · Implements PRD R16, ROADMAP M3 overlay switcher

## Context
The strip and rail never activate (invariant 7) — but a switcher with a filter box and
arrow keys cannot hear the keyboard without focus. ADR-0012 anticipated exactly this and
reserved the exception: "the M3 overlay switcher … is activatable by design."
`RailEditPopup` (ADR-0014) established the focus round trip; the switcher generalizes it.

## Decision
- A shared `ActivatableOverlayWindow` base (also the M3 picker's): borderless, topmost,
  no taskbar presence, **normally activatable**. `OpenOverlay` captures the current
  foreground window and focuses the overlay via `WindowFocuser.Focus` on its own HWND —
  a plain `Activate()` can be refused by the foreground lock. Three exits: Esc restores
  the captured window; committing (Enter/click/digit) leaves focus on the terminal the
  overlay just activated; clicking away (`Deactivated`) hides without touching focus.
  Hidden, not closed, between uses; `Closing` is cancelled outside shutdown so Alt+F4
  can't destroy the reusable HWND.
- **Keys are local WPF events, never global hotkeys** — registering Esc/arrows globally
  would swallow them system-wide. Only the open chord (`openSwitcher`, default `Ctrl+``)
  is global; pressed while open it closes like Esc.
- **Cards stay in registry order** so 1–9 mean exactly what the strip's badges say, even
  while filtered. The **MRU-previous terminal is preselected** (same list as ADR-0015),
  so `Ctrl+`` Enter` is switch-to-recent. Digits jump only while the filter is empty —
  names may contain digits, so once the user types, digits belong to the query.
- Fuzzy filtering (`FuzzyMatcher`, Core, pure) hides cards through the `ICollectionView`
  rather than rebuilding the collection; rows are upserted in place keyed by name, the
  rail's discipline, because DWM thumbnail registrations hang off them.
- Thumbnails reuse `WindowThumbnail` and the rail's sync mechanics against the overlay's
  own HWND. DWM composites them **above** all WPF visuals, so the selection ring and
  match highlights live in the card chrome around the thumb slot, never over it.
- **Hold-to-peek is deferred** — no key-up infrastructure exists (`MOD_NOREPEAT`
  hotkeys are down-only) and daily use should pick its semantics first.

## Consequences
- Invariant 7 now reads: the strip and rail never activate; `RailEditPopup` and
  `ActivatableOverlayWindow` descendants are the sanctioned exceptions.
- Filter keystrokes regenerate hidden cards' containers, churning their thumbnail
  registrations; registration is cheap and terminal counts are small, so this is
  accepted rather than designed around. Virtualization is off for the same reason.
- Esc restores focus unconditionally — opened from a browser, Esc returns to the
  browser, not the last terminal (the `RailEditPopup` precedent).
- If the foreground lock still refuses focus on some hardware, the filter box will not
  type; the fix belongs in `WindowFocuser`, not a new P/Invoke site (invariant 2).
