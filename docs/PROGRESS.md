# Project Progress

## Current Focus
All six M2 slices are code-complete (ADRs 0011–0014, plus amendments to 0002/0010/0012). Remaining for M2 exit: manual verification of Slices 4 and 6, the measured motion-sampling CPU number for ADR-0013, and daily use. M1 exit (daily-use week) runs in parallel.

## M2 Slices
- [x] Slice 1: app-bar mode (`reserveScreenSpace`) + hide over fullscreen — ADR-0011; verified by Jeremy
- [x] Slice 2: rail shell (geometry, RailWindow, hover/hotkey open-close) — ADR-0012; verified by Jeremy (found and fixed a hover-open/close bounce loop)
- [x] Slice 3: DWM live thumbnails per row — verified by Jeremy
- [x] Hover-bounce fix (JPC-TODO): with `openRailOnHover`, hover no longer expands the sliver; rail anchors at the screen edge covering the strip (ADR-0010/0012 amendments)
- [x] Slice 4: thumbnail-motion status source — ADR-0013; needs manual verify
- [ ] Manual verify Slice 4: unfocused terminal running visible output goes Working within ~1–3 s and decays to Idle ~20 s after it stops; the focused terminal and Attention terminals are unaffected; Switchboard CPU with 5 idle terminals ≈0.1% or less in Task Manager (record the number in ADR-0013's Consequences); `"thumbnailMotionEnabled": false` in the JSON turns it off live
- [x] Slice 5: adopt unbound windows (R7) — needs manual verify
- [ ] Manual verify Slice 5: open a plain `wt` window outside Switchboard, open the rail, confirm it appears under "Unbound"; "Adopt as…" a Closed registered terminal — frame colors, taskbar button hides (if enabled), status goes live, focus does not move; "Adopt as… New terminal…" registers and binds it as a new persistent entry; Refresh re-scans
- [x] Slice 6: inline rename/recolor — ADR-0014; needs manual verify
- [ ] Manual verify Slice 6: hover a rail row → pencil appears; click → popup takes focus, edit name and/or pick a swatch, Enter saves (Esc cancels) and focus returns to the window you came from; recolor repaints the frame + strip instantly and persists (~2 s); live rename keeps the window bound (jump hotkey still works, Attention survives); invalid/duplicate names show inline errors; "Adopt as… → New terminal…" opens the same popup pre-seeded; restart Switchboard after a rename → the renamed terminal recovers by handle

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

### 2026-09-05
- Slice 5 hardware bugs fixed: top-edge foot rotated to a right column (the bottom dock was crushing the card list — the "disappearing thumbnails"), first-open orientation re-applied post-layout, unbound list capped with its own scrollbar.
- Restart recovery made HWND-first (ADR-0002 amendment): binds persist `lastHwnd`; log-verified 3/3 recovered by handle where title matching failed. Rail also gained the strip's context menu (it covers the strip while open).
- Slice 6 done (last M2 slice), ADR-0014: live rename applies everywhere now — `WindowTracker.Rekey` keeps the HWND bound, `StatusEngine.Renamed` carries Attention across (new test), `TerminalSession.UpdateTerminalAsync` coordinates and persists.
- `RailEditPopup` is the product's one activatable window: pencil-on-hover rename/recolor and "Adopt as… → New terminal…", palette swatches, inline errors, focus captured and restored on close. Recolor rides the popup rather than a separate flyout (noted in ADR-0014).
- 85/85 tests; rail render smoke-verified by screenshot automation (launch → hotkey → capture).
- Next: Jeremy verifies Slices 4 and 6, records the motion-sampling CPU number in ADR-0013, then M2 exit via daily use.

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
- Slice 2 done: pure `RailPlacement` (adjacent band + off-screen hidden rect, DPI-scaled), `RailSizeDips`/`RailTopHeightDips` settings (no version bump), `RailWindow` (non-activating tool window, 220 ms slide, hidden-not-closed, mouse-leave closes — no Esc: a no-focus window can't hear keys, noted in ADR-0012), `RailViewModel` with incremental name-keyed sync (no Clear(): Slice 3 thumbnails must survive state changes), Concept A card layout with placeholder thumb slot, hover-open from strip (`openRailOnHover`/`hoverOpenDelayMs`), `Ctrl+Alt+`` bound, auto-hide suppressed while rail open. 66/66 tests. Found on hardware: the opening slide passes the rail under the cursor, so a naive MouseLeave-closes handler bounced open/closed in a loop; replaced with a 200 ms level-triggered hover probe that treats the strip as part of the rail's hover area.
- Hover-bounce fix: hovering the auto-hidden sliver no longer expands the strip when `openRailOnHover` is on (the rail is the one motion); the rail now anchors at the screen edge and covers the strip while open, so its geometry no longer depends on strip thickness and the strip may collapse freely under it. ADR-0010 and ADR-0012 amended; `RailPlacement` now accepts a zero strip offset.
- Rail context menu: the rail covers the strip while open, which made the strip's right-click menu unreachable; the rail now carries the same menu (Reapply colors / Reload registry / Quit) with matching commands on `RailViewModel`, and the hover-close probe holds while the menu is open.
- Rail feel fixes after Slice 4: thumbnails now register on the first layout pass after Show() (client-relative rects ride the slide) instead of popping in when the slide ends; motion sampling yields any tick that lands while the rail is sliding (`TerminalSession.MotionSamplingPaused`, wired to `RailWindow.IsSliding`) so PrintWindow can't stutter the 16 ms animation frames.
- Slice 4 done: `MotionSampler` in Core (eligibility: unfocused Idle/Working only; round-robin cap of 3/tick; FNV-1a; first-sample-is-baseline) + `WindowSampler` in Windows (PrintWindow `PW_RENDERFULLCONTENT` — WT is composition-rendered, plain PrintWindow is black — into a reused scratch bitmap, HALFTONE StretchBlt to 24×24, GetBitmapBits hash). Wired into the existing 1 s tick in `TerminalSession`; first caller of `StatusEngine.OutputObserved`. `thumbnailMotionEnabled` root setting (default on). 83/83 tests (11 MotionSampler, 2 OutputObserved engine cases).
- Slice 5 fixes after hardware test (verified by screenshot automation — launch, hotkey, capture): (1) the bottom-docked Unbound section ate the 150-dip top-edge rail's height, squeezing the card list to nothing ("thumbnails disappeared" — they were actually rendering, over a collapsed layout); the foot now rotates to a right-hand 250-dip column on the top edge (Concept A) and the unbound list got its own capped scrollbar. (2) First-open card orientation was wrong: the ItemsPanel doesn't exist until the window renders once, so orientation is now re-applied in the post-layout dispatch alongside thumbnail sync. (3) Restart recovery made HWND-first (ADR-0002 amendment): every bind persists `lastHwnd` into the terminal's registry row (registry is the only durable store, invariant 3; no RegistryChanged fired for it), and `WindowTracker.RecoverByHwnd` runs before `RecoverByTitle` — log-verified: second launch recovered 3/3 by handle, 0 by title, where title-matching had been failing on drifted titles ("have to click old windows to re-register"). 84/84 tests.
- Slice 5 done: `WindowTracker.UnboundWindows()` (public `UnboundWindow(Hwnd, Title)` DTO wrapping the existing `UnboundTerminalWindows()` scan — still only called on rail-open, never a timer, per R7/CLAUDE.md). `TerminalSession.AdoptAsync`/`AdoptAsNewAsync` wrap `WindowTracker.Bind` (colorize/taskbar-hide/status fire for free via `WindowAppeared`; catches the race where the window closes between listing and click). `QuickLaunchService.BuildDefinition` extracted so adopt-as-new reuses the quick-launch defaults without launching anything (the window already exists). Rail gained an "Unbound" foot section with a per-row "Adopt as…" menu (Closed registered terminals + "New terminal…", built dynamically since the offer list changes with registry state) and a manual Refresh. No new Core logic — the adopt filter depends on `WindowTracker`, so it isn't Core-testable; verified manually instead.
- Slice 3 done: `WindowThumbnail` wrapper (DwmRegisterThumbnail/UpdateThumbnailProperties/Unregister, HRESULTs logged and swallowed). Key discovery: DWM allows many independent thumbnail registrations against one destination window, each with its own rect — so the rail registers every row's thumbnail against its own HWND instead of standing up a child HWND per row, and DWM letterboxes to the source's aspect ratio automatically (no manual fit math needed). `RailWindow.SyncThumbnails` resolves each row's on-screen rect via `ItemContainerGenerator` + `DataTemplate.FindName("ThumbSlot", ...)`, converts to physical pixels via `VisualTreeHelper.GetDpi`, and re-runs on row add/remove/move, status changes, resize, and scroll; thumbnails are torn down when a row closes or the rail hides. No new Core logic (DWM-only), so no new tests.
