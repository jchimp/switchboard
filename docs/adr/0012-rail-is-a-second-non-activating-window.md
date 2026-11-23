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
  wide enough for the card row plus the foot column (now a 28-DIP menu-button slot; see the
  2026-11-22 amendment), clamped to the work area, centered in
  it. `RailPlacement` takes an optional content length (DIPs); the App layer derives it from
  the card count on each open and re-fits an open rail when cards are added or removed.
  Left/right rails are unchanged (full-height, fixed `railSizeDips`).
- *(Amended 2026-09-06)* That width is a setting, `strip.topWidth`: `full` (span the work
  area) or `centered` (default). **The strip follows the same setting**, so the color bar
  and the slide-down tray are always the same width and position — `StripPlacement.Compute`
  takes the same optional content length, and `RailMetrics` owns the one formula both
  windows call. Two consequences: the width comes from the registry's terminal count, not
  the strip's visible segments, because `showClosedTerminals` can hide segments the rail
  still lists; and app-bar mode (`reserveScreenSpace`) stays full-width regardless, since a
  reserved band spans the whole edge by nature (ADR-0011).
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

## Amendment (2026-11-22): the foot becomes a menu button

The rail had a "foot": an Unbound windows list (with "Adopt as…" and Refresh) and a
"+ New terminal" button. On the top edge the foot was a 250-DIP column at the right of the
cards. In Centered mode the strip spans the same width as the rail and splits it evenly, so
the top-edge width was `cards × 216 + 250 + 18` and each strip segment was wider than its
card. The segments did not line up with the cards. Also, since ADR-0021 the Unbound list
shows only windows of Switchboard's own portable WT. It is usually empty, so it does not
deserve permanent space.

The foot is removed. One "…" (More) button replaces it. On the top edge it has its own
28-DIP slot at the right end of the rail. It cannot overlay a card: the cards are DWM
thumbnails, and Windows composites them above WPF content. On left and right edges it sits
in a small row at the bottom of the rail. A left click opens a menu: New terminal; Adopt
window ▸ (one entry per unbound window, each with the Adopt-as choices); Settings…; Reapply
colors; Reload registry; Quit Switchboard. The unbound scan runs when the menu opens, never
on a timer. The rail's and the strip's right-click menus use the same item definitions, and
both gain New terminal.

Top edge, Centered: the width is `cards × 216 + 18 + 28`. The strip insets its segment row
by 9 DIPs on the left and 9 + 28 on the right, so each segment sits exactly over its card.
Full-width mode and the vertical edges keep the even split; exact alignment is possible
only on the top edge in Centered mode.
Two more limits: alignment needs `strip.showClosedTerminals` on (the default), because
with it off the strip drops closed segments while the rail still shows every card; and the
content width rounds *up* to whole pixels, so at 125–175 % scaling a segment can be up to
1 px off its card. Cards scrolled past the edge of the list hide their live preview, since
DWM thumbnails are not clipped by WPF and would otherwise cover the "…" slot.

Consequences: a menu is the sanctioned focus escape hatch this ADR already allows. The rail
stays open while its menu is open. Invariant 7 holds: the rail and the strip still never
activate on their own. Adopting a window now takes one more click (open the menu, then the
submenu); this is acceptable because unbound windows are rare after ADR-0021.
