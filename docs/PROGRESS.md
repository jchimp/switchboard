# Project Progress

## Current Focus
M1 code-complete plus two pulled-forward M2 items (taskbar hiding, strip auto-hide). Remaining: manual verification on real hardware, then daily use + status-rule tuning for M1 exit.

## Open Todos
- [x] First Windows build: fix compiler/analyzer errors, tests green
- [x] `Ctrl+Alt+N` opens colored WT window (verified by Jeremy)
- [x] Bring freshly launched WT window to foreground
- [x] Per-monitor placement + DPI (Core `StripPlacement`, `SetWindowPos`, re-dock triggers)
- [x] Strip state fixes (ShowClosedTerminals membership, startup foreground glow, use `Thickness`)
- [x] Strip context menu (Quit / Reapply colors / Reload registry), color reset on quit
- [x] Single-instance guard
- [x] registry.json hot-reload with self-write mute (ADR-0008)
- [x] File logging to `%LOCALAPPDATA%\Switchboard\logs` + crash handlers
- [ ] Manual verify: docking on configured monitor/edge, scale change, live JSON edits, quit menu
- [ ] Manual verify `hideFromTaskbar`: buttons vanish on bind, return on quit/toggle-off, survive Explorer restart
- [ ] Manual verify strip auto-hide: collapse to sliver, hover slide-out, Attention pulse + slide-out until focused
- [ ] M1 exit: run daily for a week; tune status rules against real WT titles
- [ ] Verify Attention fires within 2 s for an AI harness waiting on input (PRD success criterion)

## Progress Log

### 2026-09-04
- First successful Windows build: fixed TFM/platform mismatch, brace-style errors, App-layer P/Invoke leak (new `WindowStyler` wrapper), and an invalid XAML trigger target; 34 tests green. Needs x64 .NET 8 Desktop Runtime installed (only x86 was present).
- New launches now focus their window once tracked (launch-scoped, recovery binds never steal focus).
- Audited code against M1 + PRD; agreed scope: M1 checklist + daily-driver basics (quit via strip context menu, single-instance, hot-reload, file logging). ADR-0007 (physical-pixel placement, monitor-index semantics) and ADR-0008 (hot-reload) written.
- Implemented the whole slice; 51/51 tests (17 new: placement, log format/retention, watch-gate debounce/mute). Smoke launch verified: clean startup, hotkeys registered, log file written.
- Hotkeys now rebind live when `hotkeys` in the JSON changes; other JSON edits (colors, edge, monitor, terminals, rules) also apply live.
- Next: manual verification on real monitors, then daily use.
- Taskbar sprawl: `hideFromTaskbar` (global, default off) removes bound windows' buttons via `ITaskbarList::DeleteTab`, restores on unbind/quit, re-applies on `TaskbarCreated` (which also redoes DWM colors). First COM interop; new `TaskbarVisibility` wrapper. ADR-0009.
- Strip auto-hide (`autoHide`/`hiddenThickness`/`collapseDelayMs`, default off): collapses to a colored sliver, slides out on hover; Attention slides it out and holds until focused. Pulse reworked from opacity to a white-overlay color pulse (reads at 2 px, frees `Opacity` for its binding). ADR-0010; PRD R23–R25. 54/54 tests.
