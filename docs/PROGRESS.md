# Project Progress

## Current Focus
Per-user installer on branch `installer` (ADR-0023): `install.ps1` and `uninstall.ps1` ship in
the release zip. Dry runs pass on Windows PowerShell 5.1 and PowerShell 7, and the pinned WT
download, hash and zip layout were checked. A real install passed from both PowerShell
versions; upgrade, start at login, the terminal host download and uninstall are still open.

ADR-0024 (branch `terminal-host-default`): the host is installed by default and updated in
place on every run; `-NoTerminalHost` opts out. Fresh install, same version, older version
(keeps `settings\`, drops stale files), no marker, and host-running were run against a scratch
folder with the real WT zip; dry runs with and without `-NoTerminalHost` pass.

ADR-0025 (branch `first-run-defaults`): a first run with no `switchboard.json` writes every
default plus starter terminals `term-1`…`term-4` and, when the host is installed, sets the host folder to
`%LOCALAPPDATA%\Switchboard\wt`. Settings → Terminals "Import from running windows" is
removed; the rail's "Adopt as…" stays. Store tests pass; the checks below are open.

## Taskbar hide default checks (branch `taskbar-hide-default`)
`hideFromTaskbar` defaults to on for new installs (ADR-0009 amendment); changing it in Settings
now re-applies to open windows. Store tests pass; the checks below are open.
- [ ] 3 terminals open, box unticked, then tick Settings → Edge strip → "Hide bound windows from the taskbar" → all 3 buttons leave the taskbar within about 1 s
- [ ] Untick it → all 3 buttons come back
- [ ] Quit Switchboard, rename `switchboard.json`, start it → the new file has `"hideFromTaskbar": true` and the starter terminals open with no taskbar buttons
- [ ] Flag on: minimize and restore a bound window, then run `Write-Host "$([char]27)]9;4;1;50$([char]7)"` in it → no button comes back (if one does, that is review finding M-2; separate PR)

## Attention pulse checks (branch `attention-pulse-glow`)
- [x] Put a terminal in Attention → its segment pulses brighter than before (about 1.6 s per cycle) and its color glows into the segments beside it, in step with the flash
- [x] Focus it → the pulse and the glow stop; the segment shows the normal active glow
- [x] With auto-hide on and the strip collapsed, Attention → the strip stays a sliver, the pulse shows in it; the strip slides out only on hover
- [ ] With auto-hide on and the strip out (pointer on it), Attention then move the pointer away → the strip collapses after the normal delay while the terminal still pulses
- [ ] Two terminals in Attention → both pulse; CPU use for Switchboard stays low in Task Manager
ADR-0026 (branch `settings-general-startup-about`): Settings → General has a start-at-sign-in
checkbox backed by the Startup-folder shortcut, an About section, and the Settings buttons
have their own template so hover, pressed and disabled stay readable. Builds; the checks below
are open.

## Start at sign-in, About and button checks
- [ ] Tick "Start Switchboard when I sign in" → `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Switchboard.lnk` exists and points at the running exe; sign out and in → Switchboard starts
- [ ] Untick → the shortcut is gone
- [ ] Turn Switchboard off in Windows Settings → Apps → Startup, reopen Settings → the red "Windows has it turned off" line shows
- [ ] Tick from a `bin\Debug` build, then open Settings in the installed copy → the "starts …, not this copy" line shows; untick and tick → it points at the installed exe
- [ ] `install.ps1 -StartAtLogin` with Settings closed, then open Settings → the box is ticked
- [ ] About shows the release version (no `+hash`), the settings file and the log folder; both paths can be selected and copied
- [ ] Hover, press and Tab through every button on each page (Add terminal, Move up/down, Delete, ✕, + Add rule, Launch, Save, Import appearance, Open settings.json) → text stays readable, disabled Move up/Delete look dim, Tab shows a light outline

## First-run checks
- [x] Rename `%APPDATA%\Switchboard` and start Switchboard → `switchboard.json` exists at once with every setting and `term-1`…`term-4` (jump keys 1–4, distinct colors), shown closed in the rail and launchable; `terminalHost.folder` is `%LOCALAPPDATA%\\Switchboard\\wt`; Settings → General shows that path with no red error
- [ ] Same with `%LOCALAPPDATA%\Switchboard\wt` renamed too → the file is written, `terminalHost.folder` is absent, launches use the installed WT
- [ ] An existing `switchboard.json` with no folder set → not rewritten at startup, folder stays empty
- [x] Settings → Terminals has no "Import from running windows"; the rail's "Adopt as…" still lists and adopts an unbound host window

## Installer checks
- [x] `.\scripts\release.ps1 -Apply` → the zip has `install.ps1` and `uninstall.ps1` at its root
- [x] Unzip in Explorer, run `install.ps1` from Windows PowerShell → Start Menu has Switchboard, it starts with no SmartScreen prompt, Settings > Apps lists Switchboard 0.x with its icon
- [x] Same from a PowerShell 7 window (`powershell -File ...`) → no `Get-FileHash` error
- [ ] With Switchboard running and terminals open, install a newer zip → it stops, upgrades, starts again, and the terminals rebind
- [ ] `-StartAtLogin` → Startup shortcut exists; reinstall without it → shortcut is kept
- [x] `install.ps1` (no switches) on a machine with no host → `%LOCALAPPDATA%\Switchboard\wt` has `wt.exe`, `.portable` and `.switchboard-wt-version`; with the Settings > General folder empty, a launch runs from it (`Get-Process WindowsTerminal | select Path`) and the log says "(default, no folder set)"
- [ ] Run it again → "already has Windows Terminal <version>", no download
- [ ] Host installed by the old script (no `.switchboard-wt-version`), appearance imported → run with Switchboard's terminals closed → host updated, `wt\settings\settings.json` and its `.switchboard.bak` unchanged, no `wt.old-*` left
- [ ] Same with a Switchboard terminal open → warns that the terminals are open, host unchanged, the terminal keeps running; Switchboard itself still upgrades
- [ ] Offline (disable the network) on a machine with no host → warns, Switchboard still installs and starts
- [ ] `-NoTerminalHost` with no host → installs the app, warns that there is no terminal host
- [ ] Folder empty and no `%LOCALAPPDATA%\Switchboard\wt` (rename it) → launches use the installed WT as before
- [x] Type a bad folder in Settings > General while the default exists → the red "No wt.exe" error shows; it does not fall back to the default
- [ ] Uninstall from Settings > Apps → install folder, both shortcuts and the Apps entry are gone; `%APPDATA%\Switchboard` and `wt` are still there
- [ ] `-InstallDir` pointing at a folder with other files → it refuses and changes nothing

### Superseded focus (settings layout)
Settings layout and rail card polish on branch `settings-general-rail-card` (ADR-0018
amendment 2026-11-22): a General tab for the host folder and appearance import, a Terminals
tab with a list/editor splitter, remembered window size and split, and a clearer rail card.

## Settings layout and rail card checks
- [x] Settings opens on Terminals; the nav shows General first; General has the host folder, its hints, and the appearance import, all working as before
- [x] Terminals: drag the splitter both ways → the list never gets narrower than about 300 and the editor never narrower than about 330; the line turns blue on hover and while dragging
- [x] Resize the window → the list grows and shrinks, the editor keeps its width
- [x] Drag the editor wide, then shrink the window to its minimum → the editor narrows so its fields and Save stay visible; the list stays about 300
- [x] First open after a restart with a large saved size → the window is centered for that size, not pushed off the bottom/right
- [x] Close and reopen Settings, then restart Switchboard and open it again → same size and split both times; `switchboard.json` has `settingsWindow`
- [x] Open and close Settings without moving anything → `switchboard.json` is not rewritten (check its timestamp)
- [ ] Maximize, close, reopen → it opens at the size it had before maximizing
- [ ] Rail cards: name is 18 pt with no line under it; hovering a card with a folder or distro set shows them in a tooltip (no empty tooltip otherwise); the busy dot shows next to the name while working; number badge at the right edge; pencil appears to its left on hover without moving the badge
- [ ] Set Settings → Edge strip → "Rail height on the top edge" to 176 (the new default; your file keeps 190) → little space under the names, no scrollbar, every card keeps its live thumbnail, and the strip segments still line up with the cards; check at 100 % and 175 % scaling

### Superseded focus (appearance import)
Appearance import on branch `wt-appearance-import` (ADR-0022, PRD R29): Settings → Terminals
copies the installed WT's fonts, colors, opacity and schemes into the terminal host with one
button. M5 (PR #3) and the rail menu button (PR #4) are merged.

## Terminal profile panel checks (branch `terminal-profiles`)
- [x] Settings → Terminals → select a terminal: the profile field is a dropdown of the host's profiles (PowerShell, Windows PowerShell, Ubuntu, …)
- [ ] Open an existing terminal without touching the profile → Shell and Distro keep their saved values
- [ ] Pick Ubuntu → Shell becomes wsl and Distro becomes Ubuntu; pick Windows PowerShell → Shell becomes windowsPowerShell; pick Git Bash → Shell stays as it was
- [x] The panel shows command line, starting folder, font and colors; after an appearance import the font and colors match the installed WT
- [ ] Type a name WT doesn't have → red "no profile by this name" hint; the panel hides
- [ ] Set Shell to cmd, then type `WSL-dev` in the profile box: `W` auto-completes to Windows PowerShell (Shell shows windowsPowerShell for a moment), and at `WSL-dev` Shell is back to cmd
- [ ] Change Shell by hand, then pick a profile and clear it again → Shell returns to your hand-picked value
- [ ] A WSL profile whose command line is `wsl.exe -d Debian` sets Distro to Debian, not its profile name
- [x] Open settings.json → the host's file opens in your editor
- [ ] Change a profile in WT's settings, close and reopen Settings, reselect the terminal → the panel shows the change

## Appearance import checks
- [ ] Fresh host (WT's default look): Settings → Terminals shows the "uses Windows Terminal's default look" hint and the Import button
- [ ] Import → the message says how many profiles were restyled; open Switchboard terminals change font/colors within a second, no restart
- [ ] `%LOCALAPPDATA%\Switchboard\wt\settings\settings.json.switchboard.bak` exists and is the original file (comments intact); the hint is gone after import
- [ ] Launch every Switchboard terminal after import → each still runs its own command and directory; tab colors are still Switchboard's
- [ ] Change the installed WT's font, import again → the host follows (overwrite), and the `.bak` still holds the original, not the first import
- [ ] Close and reopen Settings after the host WT first creates its settings.json → the import section appears without restarting Switchboard
- [ ] Clear the host folder → the import section disappears

### Superseded focus (rail menu)
Rail menu button (ADR-0012 amendment 2026-11-22), merged as PR #4.

## Rail menu checks
- [x] Top edge, Centered, Show closed terminals on, with 1, 5 and 9 terminals, at 100 % and 175 % scaling: each strip segment sits over its rail card (within 1 px), and no horizontal scrollbar appears
- [x] The "…" button is never covered by a thumbnail, with any card count; with more cards than fit (side edge, or 9+ on a 1920-px top edge), scrolled-off cards show no live preview and scrolling brings it back
- [x] Click "…" → the menu opens; the rail stays open; the terminal you were typing in keeps focus after the menu closes (Esc or click-away)
- [x] Menu → New terminal → the rail closes and a terminal launches with the quick-launch defaults (no picker; same as the old "+ New terminal" button)
- [x] With the "…" menu open, click "…" again → the menu closes and does not reopen
- [x] Menu → Adopt window ▸ lists only unbound windows of the private-host WT (open one with Ctrl+Shift+N in a Switchboard terminal); a Store WT window never appears; adopting one binds and colors it
- [x] Right-click the strip and right-click the rail → both menus show the same items, including New terminal
- [ ] Left and right edges: the "…" button sits in a small row at the bottom of the rail and the menu works
- [x] Top edge, Full width: strip and rail still look right (even segment split, no gaps or overlap)

### Superseded focus (M5)
M5 (private terminal host) code-complete on branch `private-terminal-host`: no adoption by
title (ADR-0020); terminals launch from a portable WT and only its windows can be bound,
adopted or imported (ADR-0021). 163/163 tests. Spike passed (separate process, settings in
`wt\settings\settings.json`); everything below needs hardware checks.

## M5 Checks
- [ ] Spike leftover: Store WT on `"windowingBehavior": "useExisting"`, a Switchboard terminal open → `wt` from Start opens in a Store window, never as a tab in the Switchboard one
- [x] Profiles in the terminal host: none to copy. The portable WT generates the same dynamic profiles (PowerShell, Ubuntu, Git Bash, …) on first run, and launches go by profile name. Only appearance differs (font, colors, opacity); importing it is the next item of work
- [x] Settings → Terminals: set the host folder to `%LOCALAPPDATA%\Switchboard\wt` → no red error; type a bad path → error shows; clear it → error goes
- [x] Strip click launches each terminal in the portable WT (`Get-Process WindowsTerminal | select Id,Path` shows the portable path); colors, status rules, thumbnails, attention pulse, hide-from-taskbar all still work
- [x] Open a Store WT window and retitle it to a registered name (`$Host.UI.RawUI.WindowTitle = 'anvil'`), restart Switchboard → it is not bound, not colored, keeps its taskbar button
- [x] Rail "Unbound" and Settings import list only portable-WT windows (open one with Ctrl+Shift+N inside a Switchboard terminal); Store WT windows never appear
- [x] Restart Switchboard with terminals running → all rebind by handle (log: "Recovered N windows by handle")
- [x] Ctrl+Alt+N picker offers the portable WT's profiles

### Superseded focus (M4)
M4 code-complete on branch `m4-settings-ui`: the settings window (Terminals / Edge strip /
Hotkeys / Status rules), stolen-chord badges, and bulk import from running windows — ADR-0018,
143/143 tests. Rendering and live-apply were verified by screenshot automation; the four slice
checklists below still want a human. M3 is merged to main. Still open from M2: manual
verification of Slices 4 and 6, the motion-sampling CPU number for ADR-0013, and daily use.

### Superseded focus (M3)
M3 code-complete on branch `m3-switcher-picker` (built on `rail-stacked-thumbs`, which still awaits its own visual check + merge): overlay switcher, quick-launch picker, MRU cycle-recent — ADRs 0015–0017, 119/119 tests. Since then, top-edge width became a setting (`strip.topWidth`, strip and rail together) and `tools/Release.ps1` landed. All M3 slices plus the two width modes need hardware verification below. Still open from M2: manual verification of Slices 4 and 6, the motion-sampling CPU number for ADR-0013, and daily use.

## M4 Slices
- [x] Slice 1: settings shell + Terminals page (table, editor, add/delete/reorder) — ADR-0018; render-verified by automation
- [ ] Manual verify Slice 1: open from the strip menu → the window is in the taskbar and Alt-Tab, resizes, dark title bar; rows match the strip's order and colors and the status dots track live state; rename a terminal → Save → the window stays bound (jump hotkey still works), strip relabels; a duplicate name and a bad slug both show inline and do not save; Delete removes it from strip and JSON and its window keeps its taskbar button; Move up/down reorders the strip; close the window → focus is *not* yanked anywhere
- [x] Slice 2: Edge strip + Status rules pages, live-apply on a 400 ms debounce (`SettingsApplyQueue`, `SettingsBounds`, `StatusRule.ValidatePattern`) — verified end-to-end: two toggles reached switchboard.json with the terminal list intact and a `.bak` kept
- [ ] Manual verify Slice 2: change edge → the strip moves within a second, no restart; drag Thickness → the strip grows and the file settles ~400 ms after you stop; push Sliver thickness above Thickness → clamped, the strip never disappears; turn on Reserve screen space → maximised windows stop at the strip and the auto-hide note appears; add a global rule and watch a matching title pulse; type an unbalanced `(` → inline error and nothing written; **hand-edit the JSON while the window is open → the page re-syncs and does not fight you**
- [x] Slice 3: Hotkeys page with chord capture + per-chord Bound/Taken/Unparseable badges (`HotkeyBindings.Status`) — render-verified; closes the "surfacing stolen hotkeys" deferral
- [ ] Manual verify Slice 3: every chord lists with a correct badge; rebind `openSwitcher` off ``Ctrl+` `` (the VS Code conflict) → the new chord opens the switcher immediately and ``Ctrl+` `` returns to VS Code, no restart; bind something Windows owns → the row says "taken", nothing crashes; change `jumpPrefix` → 1–9 follow it; restart → still bound
- [x] Slice 4: bulk import from running windows (`TerminalSession.ImportAsync`, `NameSlug`, import dialog) — dialog verified against a real unbound window; **the commit path itself is not yet exercised**
- [ ] Manual verify Slice 4: open three plain `wt` windows → all three listed with sane names and distinct colors; uncheck one, Import → two are registered, colored, tracked and on the strip, the third untouched; exactly one save in the log; close a window mid-dialog then Import → that row reports "window is gone" and the rest still land; a colliding name shows inline; restart → imported terminals recover by HWND

## M3 Slices
- [x] Slice 1: MRU list + cycle-recent walk (`MruList` in Core, frozen-snapshot sessions) — ADR-0015; needs manual verify
- [ ] Manual verify Slice 1: focus A, B, C in order; one `Ctrl+Shift+\`` lands on B; tap again quickly → A; pause 2 s, tap once → back to C (the walk committed). Close a terminal in the chain → cycling skips it. Fresh start → first tap is a no-op until two terminals have been focused
- [x] Slice 2: `Ctrl+\`` overlay switcher (card grid, DWM thumbnails, fuzzy filter, arrows/1–9/Enter/Esc, MRU preselect) — ADR-0016; needs manual verify
- [ ] Manual verify Slice 2: `Ctrl+\`` from a terminal → centered grid, numbers match strip badges, previously-focused card preselected, bare Enter = switch-to-recent; **first check the filter box actually types** (foreground-lock risk); type a fragment → cards filter with matched letters bright, no thumbnail flicker; digits jump only while the filter is empty; Esc restores the exact prior window; click-away hides; selecting a Closed terminal relaunches it; open/close 20× → no leaks; `Ctrl+\`` while open closes
- [x] Slice 3: `Ctrl+Alt+N` picker + WT profile enumeration (`WtSettingsReader` Core, `WtProfileProvider` App) — ADR-0017; needs manual verify
- [ ] Manual verify Slice 3: picker shows next `term-N`, default profile chip selected, keep per `persistByDefault`; bare Enter launches exactly what the quiet chord does; Tab walks the real WT profiles (hidden ones absent — compare WT's own dropdown); pick a WSL profile + Enter → colored, tracked window; uncheck keep (Ctrl+K) → row vanishes from the registry when the window closes; Esc restores focus; `Ctrl+Alt+Shift+N` launches with zero UI; rename WT's settings.json temporarily → picker still opens with only the default profile, warning logged
- [x] Slice 4: last-directory memory (`quickLaunch.lastDirectory`, quiet-save) — rides ADR-0017; needs manual verify
- [ ] Manual verify Slice 4: launch via picker with `C:\src` in the directory box → shell opens there; `switchboard.json` gains `lastDirectory` within ~2 s with no reload flicker; next picker open prefills it; the quiet chord lands there too; delete the folder → launch still succeeds in the profile default, warning logged; hand-edit `lastDirectory` → next launch honors it

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
- [ ] Visual check of `rail-stacked-thumbs` branch (both edges), then merge to main
- [ ] Update `railTopHeightDips` 150 → 190 in the live switchboard.json (old value clips the top-edge cards)
- [x] Rename `registry.json` → `switchboard.json`, with a one-time read-fallback to the old name
- [ ] Manual verify `strip.topWidth`: default `centered` → bar and rail same width and centered, both re-fit when a terminal is added/removed; set `"full"` → both span the screen, no restart needed; with `autoHide` on, the collapsed sliver stays centered at the same width (only thickness animates); confirm `reserveScreenSpace` still reserves a full-width band in either mode
- [ ] Manual verify the rename: restart Switchboard with the old file in place — same terminals, `switchboard.json` appears beside an untouched `registry.json`, hot-reload still fires on a hand-edit

## Progress Log

### 2026-09-08 (M4)
- M4 planned with Jeremy and built on `m4-settings-ui`. Four decisions taken up front: the UI
  edits only settings that exist in `RegistryFile`; live-apply rather than Save buttons; the
  named-pipe source leaves M4 (moved to M5); bulk import is in. ADR-0018 records all of it.
- Consequence of decision 1: the mockup's **Appearance page is not built** — every control on
  it (title prefix, dim-unfocused, tab color, thumbnail size, theme) has no backing field. The
  Status page likewise omits the mockup's taskbar-flash and named-pipe toggles: `Flashed` has
  no caller and the pipe does not exist, so both would have been lies.
- The settings window is the first activatable window that behaves like a normal app —
  taskbar, Alt-Tab, resize, and no focus restore on close. Native chrome plus
  `DWMWA_USE_IMMERSIVE_DARK_MODE` (one new constant, existing P/Invoke) instead of
  reimplementing the mockup's custom titlebar.
- `TerminalSession` gained the three writes the UI needed and nothing more:
  `ApplySettingsAsync` (settings only — it carries the terminal list over untouched so a
  settings edit cannot stomp a concurrent rename), `ReorderTerminalsAsync`, and `ImportAsync`
  (whole batch, one save, per-row failures returned).
- `SettingsApplyQueue` is the live-apply path: edits fold into one pending registry on a 400 ms
  timer, and `IsApplyingOwnEdit` stops a page reloading from the `RegistryChanged` its own save
  raised — without it the round trip overwrites the box you are typing in.
- Three bugs found by driving the real UI rather than trusting the build: fixed columns ate the
  whole table so the Name column collapsed to nothing; `RowBackground="Transparent"` is a local
  value that beat the IsSelected trigger, so the selected row never highlighted; and every page
  rendered on top of every other because `Visibility` and `DataContext` were set on the same
  element, so `IsStrip` resolved against the page view model and the binding failed silently.
- The nav started as RadioButtons with a Command; UIA could select one without the page
  changing, which is also what a keyboard user would have hit. It is a ListBox bound to the
  page now, so the selection *is* the state and arrow keys work.
- `%APPDATA%` cannot be sandboxed for tests: `RegistryPaths` goes through
  `Environment.GetFolderPath`, which ignores the env var. A verification run therefore wrote
  two settings into the live registry; both were restored, and later runs back the file up
  first. Worth knowing before anyone writes another UI test.
- 132 → 143 tests (`SettingsBoundsTests`, `StatusRuleValidationTests`, `NameSlugTests`).
  Build and `dotnet format` clean.
- Not verified by a human: the four slice checklists above. The import *commit* in particular
  has only had its dialog exercised, not its write.

### 2026-09-06 (M3)
- M3 planned with Jeremy and implemented in one pass on `m3-switcher-picker` (branched off `rail-stacked-thumbs` so it carries the rename + rail restyle). Decisions: profiles read live from WT's settings.json (not a curated list); switcher grid in registry order with MRU preselect; hold-to-peek deferred.
- Slice 1 (ADR-0015): `MruList` in Core (`Touch`/`Forget`/`Rename`/`Prune`); `TerminalSession` drops `_previousForegroundName` for it and `CycleRecentAsync` walks a snapshot frozen at the first tap — 1.5 s settle timer, skip dead entries, commit-touch only the landing terminal, abort on any outside foreground change. Runtime-only, empty after restart.
- Slice 2 (ADR-0016): `ActivatableOverlayWindow` base (capture foreground → `WindowFocuser.Focus` own HWND → Esc restores / commit doesn't / Deactivated hides silently; Closing cancelled outside shutdown so Alt+F4 can't kill the reusable HWND). `SwitcherWindow` copies the rail's thumbnail sync verbatim; `SwitcherViewModel` copies `RailViewModel.Sync()`'s upsert-in-place; filter = `ICollectionView` over `FuzzyMatcher` (new in Core, subsequence + spans for highlight). Digits jump only while the filter is empty — names may contain digits.
- Slice 3 (ADR-0017): `WtSettingsReader` (Core, pure, JSONC-tolerant, both `profiles` shapes, hidden skipped, never throws) + `WtProfileProvider` (App: packaged/Preview/unpackaged paths, re-read per open). Picker seeds fully from defaults so bare Enter ≡ the old launch; new `newTerminalQuiet` chord (`Ctrl+Alt+Shift+N`) keeps the zero-UI path. Chips launch by name, never guid.
- Slice 4: `quickLaunch.lastDirectory` (additive, no version bump) + `TerminalSession.RememberLastDirectoryAsync` via the `RememberHwnd` quiet-save path. Fallback applies only on actual launches — adopt seeds don't inherit a directory. Nonexistent dirs dropped with a warning, never a failed launch; WSL paths skip the check.
- 108/108 tests (20 new: MruList 6, FuzzyMatcher 7, WtSettingsReader 6, store round-trip 1). CLAUDE.md staleness fixed (`RegisterAll` → `Rebind`, milestone section); PRD R16/R21/R22 and ROADMAP M3 updated.
- Not verified on hardware: all four M3 slice checklists above. First thing to check is that the switcher's filter box actually types (foreground-lock risk, ADR-0016 Consequences).
- Slide-animation smoothness pass (03758ff): both slide timers were Background-priority DispatcherTimers, starved exactly when the rail's opening slide triggers layout/thumbnail work; new shared `WindowSlide` ticks on `CompositionTarget.Rendering` (vsync-aligned). Rail's position-only slide now passes `SWP_NOSIZE` (skips layered-window surface reallocation); mid-slide reversals start from the actual current rect instead of snapping. Feel (220 ms ease-out cubic) unchanged.
- Slide smoothness verified by Jeremy on hardware ("much better").
- Top-edge geometry is now content-width and centered, and it's a setting: `strip.topWidth` = `full` | `centered` (default centered, no version bump). The strip follows the same setting as the rail, so the color bar and the slide-down tray always match — `StripPlacement.Compute` gained the rail's optional content-length parameter and a new App-side `RailMetrics` owns the one width formula both windows (and the XAML card template) read.
- Two calls worth knowing: the width comes from the registry's terminal count, not the strip's visible segments, because `showClosedTerminals` can hide segments the rail still lists (so the strip now re-docks on `RegistryChanged` too); and app-bar mode stays full-width regardless, since a reserved band spans the whole edge by nature. ADR-0012 amended twice.
- New `tools/Release.ps1`: publishes Release to `dist/Switchboard-v<version>/`, version from `-Version` or the csproj, refuses to overwrite without `-Force`. `dist/` gitignored. Exercised end-to-end with a throwaway version.
- 119/119 tests, build and `dotnet format` clean. Not verified on hardware: the two width modes.

### 2026-09-06
- Registry file renamed `registry.json` → `switchboard.json` (JPC-TODO). Filename only: "registry" stays the product word, so `IRegistryStore`, `RegistryFile`, `RegistryWatcher` and the Core namespace are untouched.
- Existing configs are adopted, not stranded: the store reads the old name when the new one is absent, and `TerminalSession.InitializeAsync` writes the new file immediately rather than at the next save — otherwise the watcher would sit on a file that doesn't exist yet. Old file left in place as a de facto backup. ADR-0008 amended.
- `%APPDATA%` moved out of Core into a new App-side `RegistryPaths`, so `JsonRegistryStore` now takes only strings — invariant 1 was being broken by the old `DefaultPath` static.
- 88/88 tests (3 new: adoption, save-after-adoption keeps the old file, new file wins when both exist). Docs, PRD, README, ADR-0008 and `docs/switchboard.example.json` renamed to match.
- Not verified on hardware: Switchboard was running and holding the build output, so the App was compiled to a scratch dir to prove it builds. Needs a restart to exercise the real adoption path.
- Next: unchanged — the standing Slice 4/6 verifications and the rail-stacked-thumbs visual check.

### 2026-09-05
- Rail card restyle on branch `rail-stacked-thumbs` (dbfe346): stacked layout — name/badge/pencil under a full-width 204×115 thumbnail (was 76×46 side-by-side), 16:9 so DWM stops letterboxing; "Terminals" header row removed.
- Constraint learned: DWM composites live thumbnails above the rail's own visuals, so nothing can overlay the thumb slot — the jump-key badge moved to the name row (mockup's overlay idea is dead for live thumbs).
- `RailTopHeightDips` default 150 → 190 (fits one stacked card; no version bump); mockup `layout-concepts.html` updated to match the XAML.
- Build clean, 85/85 tests; app smoke-launched OK. Not yet visually verified — and Jeremy's live registry still serializes 150, which will clip the top-edge rail until edited.
- Next: Jeremy eyeballs the new rail on both edges, merges the branch, then the standing Slice 4/6 verifications.
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
