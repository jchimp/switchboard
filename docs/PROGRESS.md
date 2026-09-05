# Project Progress

## Current Focus
M2 underway, slice-by-slice with manual verification between slices (plan approved 2026-09-04). Slice 1 (app-bar mode + hide over fullscreen, ADR-0011) is code-complete and awaits manual verification. M1 exit (daily-use week) runs in parallel.

## M2 Slices
- [x] Slice 1: app-bar mode (`reserveScreenSpace`) + hide over fullscreen — ADR-0011; verified by Jeremy
- [x] Slice 2: rail shell (geometry, RailWindow, hover/hotkey open-close) — ADR-0012; needs manual verify
- [ ] Manual verify Slice 2: hover strip ~250 ms opens rail (Ctrl+Alt+` toggles); rows show name/sub/key, closed dimmed, active outlined; row click focuses + closes; mouse-leave closes; "+ New terminal" works; no terminal ever loses focus while the rail opens; auto-hide strip stays out while rail open
- [ ] Slice 3: DWM live thumbnails per row
- [ ] Slice 4: thumbnail-motion status source — ADR-0013 first
- [ ] Slice 5: adopt unbound windows
- [ ] Slice 6: inline rename/recolor — ADR-0014 first

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
- [x] Manual verify: docking on configured monitor/edge, scale change, live JSON edits, quit menu
- [x] Manual verify `hideFromTaskbar`: buttons vanish on bind, return on quit/toggle-off, survive Explorer restart
- [x] Manual verify strip auto-hide: collapse to sliver, hover slide-out, Attention pulse + slide-out until focused
- [ ] M1 exit: run daily for a week; tune status rules against real WT titles
- [x] Verify Attention fires within 2 s for an AI harness waiting on input (PRD success criterion)

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
- Attention review: title match is the only live path (`StatusEngine.Flashed` is dead code — no BEL/flash detection wired); default trigger words are broad, expect false positives.
- Fixed: attention raised on the already-focused window now clears immediately instead of pulsing until refocus. Added sticky test for the title-driven path; 55/55 tests.
- New `tools/Test-Attention.ps1`: manual end-to-end pulse test (retitles the terminal with a trigger word after a delay).
- M2 planned: six slices, four new ADRs (0011–0014). Decisions: rail is a second non-activating window with a focusable edit popup; live rename applies immediately with unbound-after-restart as the degraded path; motion source polls PrintWindow hashes on the existing 1 s tick (default on); adopt list populated on rail-open only. No registry version bump anywhere.
- Slice 1 done: `AppBar` wrapper (ABM_NEW/QUERYPOS/SETPOS negotiation from full monitor bounds — new `MonitorBounds.Bounds` + pure `ComputeAppBar`, 3 new tests), `FullscreenDetector` (foreground-event-driven, same-monitor only, desktop classes excluded), strip hides via `Visibility` on ABN_FULLSCREENAPP (app-bar path) or foreground checks (otherwise). 58/58 tests. Verified by Jeremy (autoHide correctly yields to reserveScreenSpace per ADR-0010).
- Slice 2 done: pure `RailPlacement` (adjacent band + off-screen hidden rect, DPI-scaled), `RailSizeDips`/`RailTopHeightDips` settings (no version bump), `RailWindow` (non-activating tool window, 220 ms slide, hidden-not-closed, mouse-leave closes — no Esc: a no-focus window can't hear keys, noted in ADR-0012), `RailViewModel` with incremental name-keyed sync (no Clear(): Slice 3 thumbnails must survive state changes), Concept A card layout with placeholder thumb slot, hover-open from strip (`openRailOnHover`/`hoverOpenDelayMs`), `Ctrl+Alt+`` bound, auto-hide suppressed while rail open. 66/66 tests.
