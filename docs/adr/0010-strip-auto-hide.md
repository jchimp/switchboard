# ADR-0010: The strip can auto-hide to a sliver; Attention pulses color and slides it out

**Status:** Accepted · 2026-09-04 · Amends ADR-0006

## Context
ADR-0006 says the strip "must be visible at all times". Even at 8 px, some users (and some monitors) want the edge back. But fully hiding the strip kills the glance layer — its whole job is showing state without being asked.

## Decision
- Opt-in `autoHide` (default off) in strip settings. When the cursor leaves the strip, it collapses after `collapseDelayMs` to a `hiddenThickness` (default 2 px DIP) sliver; touching the screen edge slides it back out. The sliver is the same window at reduced thickness, so segment colors — most of the glance value — survive collapse.
- Collapse/expand geometry is computed by the existing pure `StripPlacement.Compute` with the alternate thickness (ADR-0007 still holds: Core computes, physical pixels applied via `SetWindowPos`).
- **Attention overrides auto-hide** (withdrawn 2026-11-24, see the amendment below): while any terminal is in Attention, the strip slides out to full thickness and stays out until the terminal is focused (invariant 6: attention is sticky). This is permitted unprompted motion under invariant 10 — attention is the one thing allowed to move.
- The attention pulse becomes a **color** pulse (segment color ↔ lightened variant) instead of an opacity pulse, implemented as a white overlay fading in and out above the segment. It reads at 2 px where an opacity dip would not, per-item brushes stay frozen, and it removes a latent conflict where the old animation held `Opacity` against its view-model binding.

## Amendment (2026-09-04, with ADR-0012)
When `openRailOnHover` is on, hovering the sliver no longer expands the strip — expanding
it and then sliding the rail out read as two chained bounces. The sliver is only the
trigger surface; the rail is the single motion, and it covers the strip from the screen
edge while open. Attention still expands the strip (R24). With `openRailOnHover` off,
hover-expand behaves as originally decided.

## Amendment (2026-11-24): a stronger pulse with a color glow, and no slide-out
The pulse was easy to miss: a linear 0 to 0.45 overlay over 0.6 s. It now peaks at 0.7 over
0.8 s with a sine ease, so it lingers at the bright end, and the same storyboard grows the
segment's own-color glow from 0 to 16 px in step. The glow spreads sideways into the
neighbouring segments and reads from the corner of the eye. It is still one pulse
(invariant 10).

The same amendment **withdraws "Attention overrides auto-hide"**. A collapsed strip no longer
slides out for Attention, and an expanded strip no longer stays out for it: the strip moves
only when the cursor reaches it. The pulse and glow play in the sliver instead. The old
slide-out was a second unprompted movement on top of the pulse and pulled the eye harder
than the user wanted; with the brighter pulse and the glow, the sliver carries the signal.
Attention is still sticky until the terminal is focused (invariant 6); only the slide is
gone. The active segment's glow is static, and an attention segment is never the
active one, so the two do not mix. The blur is redrawn each frame while attention lasts; the
cost is small at 10 segments or fewer.

## Consequences
- ADR-0006's "visible at all times" is relaxed to "a colored sliver is visible at all times" when auto-hide is on.
- Auto-hide and app-bar mode (`reserveScreenSpace`) are alternatives; enabling both is allowed but the reserved band shrinks to the sliver only if the user accepts the app-bar re-registering on every collapse — v1 simply ignores `autoHide` when app-bar mode is on.
- Each segment carries one extra (normally invisible) overlay element; negligible cost at ≤ 10 segments.
