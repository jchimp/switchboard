# ADR-0009: Bound windows are hidden from the taskbar via ITaskbarList, opt-in

**Status:** Accepted · 2026-09-04

## Context
Taskbar crowding is a founding problem (PRD problem 2), but nothing in the design removes the WT buttons — six terminals still mean six taskbar entries (or one crowded group). Options considered:

1. Write our own terminal and own the windows — total control, absurd cost, and a non-goal (we sit *above* WT).
2. Set `WS_EX_TOOLWINDOW` on the foreign WT window — removes the button, but also removes the window from Alt-Tab, and mutating another process's window styles is fragile (WT/DWM can reset or misrender).
3. `ITaskbarList::DeleteTab(hwnd)` — asks the shell to drop the button. The window is untouched: still visible, still in Alt-Tab, still fully functional.

## Decision
- One global `hideFromTaskbar` flag in the registry, **default off**. Not per-terminal: the point is killing sprawl wholesale, and the strip/hotkeys are the switching surface for every bound terminal anyway.
- When on, `DeleteTab` is applied at bind time (launch, recovery, adopt) and `AddTab` restores the button on unbind, on the flag turning off (hot reload), and on Switchboard exit. Unbound WT windows are never touched.
- Explorer restarts recreate all buttons; we listen for the shell's `TaskbarCreated` broadcast and re-apply (colors too — DWM attributes are also lost).
- The COM interface lives in `NativeMethods.cs` (invariant 2) behind a typed wrapper, `TaskbarVisibility`, on the `FrameColorizer` pattern: UI-thread only, failures logged, never thrown.

## Consequences
- A minimized hidden window is reachable only through Switchboard or Alt-Tab. That is the product working as intended, but it is why the flag defaults off.
- If Switchboard crashes, buttons stay hidden until the next Explorer restart or Switchboard relaunch. Windows remain fully usable meanwhile.
- On Windows 11 "combine taskbar buttons" the sprawl is already one icon; this flag mainly serves "never combine / show labels" setups.
