# ADR-0019: Clicking the focused terminal minimizes it

**Status:** Accepted · 2026-09-22 · Amends PRD R13, extends ADR-0006 and ADR-0012

## Context
R13 made a strip segment click mean "focus that terminal, or launch it if closed". A click on
the segment of the terminal that is *already* foreground therefore did nothing, which wastes
the most natural gesture for "get this out of my way". Taskbar buttons have had click-to-
minimize forever, and with `hideFromTaskbar` on (ADR-0009) the strip is the only button the
terminal has.

The strip and rail never activate (invariant 7, ADR-0006, ADR-0012). That is what makes the
gesture cheap to detect: when the click lands, the foreground window is still whatever it was
before, so "is the clicked terminal foreground?" is one `GetForegroundWindow` compare.

## Decision
- **A strip segment or rail thumbnail click on the foreground terminal minimizes it.** Any
  other click keeps its R13 meaning. The next click restores and focuses, because
  `WindowFocuser.Focus` already `SW_RESTORE`s iconic windows.
- **It is a setting**, `strip.clickMinimizesFocused`, default on, exposed as a checkbox on the
  Edge strip page and applied live (ADR-0018). Turning it off restores the pre-0019 behaviour.
- **Only clicks toggle.** `TerminalSession.ActivateAsync` takes `toggleMinimize`, and only the
  strip and rail pass true. Jump hotkeys, cycle-recent and the switcher always focus; a hotkey
  that sometimes hides the window you asked for would be a surprise.
- **Attention is untouched.** Minimizing does not clear or set Attention (invariant 6).

## Consequences
- `NativeMethods` gains `SW_MINIMIZE`; `WindowFocuser` gains `IsForeground` and `Minimize`.
- Minimized windows stop being sampled for thumbnail motion (ADR-0013 already excludes them).
- With `hideFromTaskbar` on, a minimized terminal is reachable only through Switchboard or
  Alt-Tab, as ADR-0009 already notes. The strip click brings it back, so nothing is stranded.
- The foreground check is Win32-only and lives in the App/Windows projects; Core tests cover
  the setting's default and round-trip only.
