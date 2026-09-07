# ADR-0012: The rail is a second non-activating window; only the edit popup ever takes focus

**Status:** Accepted · 2026-09-04 · Extends ADR-0006

## Context
M2 adds the rail (PRD R15): hover or `Ctrl+Alt+`` slides out a panel of rows — name, shell,
working dir, jump key, and (Slice 3) a live thumbnail per terminal. The rail exists to glance
and to click-jump; stealing focus from the terminal the user is typing in would be the exact
failure the strip's invariant 7 exists to prevent. But M2 also promises inline rename and
"adopt as new…", which need a text box, and a text box needs keyboard focus.

## Decision
- The rail is its own top-level window with the strip's exact non-activation contract:
  `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, `ShowActivated=False`, `Focusable=False`, topmost,
  no taskbar presence. Everything on it — click-to-jump, adopt-from-list, recolor swatches —
  is mouse-only and works without activation.
- Rename and "adopt as new" open a small activatable popup window (`RailEditPopup`, Slice 6),
  anchored to the row. It takes focus only because the user clicked an edit affordance —
  the same sanctioned escape hatch as the strip's context menu — captures the previous
  foreground window first, and restores it when it closes.
- The rail opens on strip hover (`openRailOnHover` + `hoverOpenDelayMs`) and on the
  `toggleRail` chord; it closes when the mouse leaves it or on the chord again. There is
  no Esc-to-close: a window that never has keyboard focus cannot hear Esc, and registering
  Esc as a global hotkey would swallow it system-wide. The mockup's Esc affordance moves to
  the M3 overlay switcher, which is activatable by design.
- Clicking a strip segment keeps its R13 meaning (focus that terminal); it does not toggle
  the rail. Hover and the chord are the only openers.
- Geometry is pure Core (`RailPlacement`): a band on the strip's edge — vertical panel for
  left/right, full-width drop-down for top (ADR-0006's rationale for the top default). The
  Windows layer applies physical pixels via `SetWindowPos` (ADR-0007).
- *(Amended 2026-09-06)* The top-edge rail is **content-width and centered**, not full-width:
  wide enough for the card row plus the foot column, clamped to the work area, centered in
  it. `RailPlacement` takes an optional content length (DIPs); the App layer derives it from
  the card count on each open and re-fits an open rail when cards are added or removed.
  Left/right rails are unchanged (full-height, fixed `railSizeDips`).
- *(Amended 2026-09-04)* The rail anchors at the **screen edge and covers the strip while
  open** — exactly the Concept A mockup. Its geometry never depends on the strip's current
  thickness (sliver, full, or attention-expanded), the strip needs no held-open state while
  the rail is out, and with `openRailOnHover` on, hover doesn't expand the sliver at all
  (the two chained animations read as a bounce; see the ADR-0010 amendment).

## Consequences
- Two always-alive windows instead of one; the rail is hidden, not closed, between uses.
- All rail interactions must remain mouse-complete; anything needing typing goes through
  the popup, keeping invariant 7 auditable in one place.
- Slide-out is user-initiated (hover/hotkey), so invariant 10 (motion only for attention)
  holds without exception.
