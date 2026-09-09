## JEREMY-TODO — Switchboard review queue

Personal working notes for Jeremy (not for the engine). Skim top-to-bottom.

### 🔴 Decisions I need from you (I made a call; confirm or override)

- [ ] **M4: the mockup's Appearance page is not built.** You chose "only settings that exist
  today", and every control on that page (title prefix, dim-frame-when-unfocused, tab-color
  toggle, rail thumbnail size, theme) has no field behind it. Same reason the Status page has
  no taskbar-flash or named-pipe toggle: `StatusEngine.Flashed` has no caller and the pipe
  isn't implemented. The mockup and the shipped window now visibly differ — ADR-0018 is the
  record of why. Say the word if you'd rather have any of those settings for real.
- [ ] **The named-pipe status source moved from M4 to M5.** Your call in planning. PRD R19 no
  longer promises it in M4 and the ROADMAP bullet moved.
- [ ] **Settings pages apply live; only the terminal editor has a Save button.** A name change
  rebinds a live window (ADR-0014), which is not something to do per keystroke; everything
  else writes ~400 ms after you stop fiddling. Consequence: there is no Cancel — closing the
  window keeps what you changed, exactly like hand-editing the file.
- [ ] **A verification run wrote to your live switchboard.json.** `%APPDATA%` can't be
  redirected for the app (`GetFolderPath` ignores the env var), so a test that I thought was
  sandboxed toggled `hideOverFullscreen` and `showClosedTerminals` off in your real config. I
  set both back to `true` and later runs back the file up first. Worth a glance at your strip
  settings to confirm they're as you left them.

- [ ] **M3 sits on top of `rail-stacked-thumbs`, not main.** You said "branch before commit"; your checkout was on `rail-stacked-thumbs` (which still awaits your rail visual check), so I branched `m3-switcher-picker` from it — M3 needs the switchboard.json rename that lives there, and branching from main would have meant a conflict-prone rebase later. Consequence: merging M3 brings the rail restyle and the rename with it. Confirm that's fine, or tell me to rebase M3 onto main after you've merged (or rejected) the rail branch.
- [ ] **In the switcher, digits 1–9 jump only while the filter box is empty.** The plan said "digits jump"; I noticed terminal names may legally contain digits (`term-2`), so a blanket digit-jump would make those names untypeable in the filter. Once you've typed anything, digits go to the query instead. The alternative is digits-always-jump (purer alt-tab feel, filter loses digit names). It's one condition in `SwitcherWindow.Window_PreviewKeyDown` either way — say the word to flip it.
- [ ] **The picker's "keep" toggle is Ctrl+K, not Space.** The plan sketched Space-toggles-when-checkbox-focused, but the directory box holds focus and paths contain spaces, so Space must type. Ctrl+K works from anywhere in the picker; the checkbox is also clickable. Confirm, or pick another chord.
- [ ] **Closing a terminal's window drops it from the MRU immediately.** So cycle-recent and the switcher's preselect only ever offer live windows, and a relaunched terminal re-earns its recency from zero. The alternative — keeping closed terminals in the MRU so cycling could relaunch them — would let a 15 s launch-and-bind stall a quick cycle tap, which is why I didn't. ADR-0015 records it. Confirm the semantics.
- [ ] **Monitor index semantics: 0 = primary, 1..N = secondaries ordered left-to-right.** `"monitor"` in switchboard.json was never read before, so I had to define what the number means. I picked primary-first with position ordering so the value survives Windows' arbitrary enumeration order and an unplugged monitor falls back to primary instead of erroring. The alternative is raw enumeration order (what most tools do), which silently remaps when you dock/undock. Written up in ADR-0007. Confirm the semantics, or tell me to use enumeration order.
- [ ] **The strip got thinner: it now renders at `thickness` (8 px) instead of `activeThickness` (14 px).** The old code used the wrong setting; `activeThickness` is reserved for M2's active-segment growth, which I deferred entirely (invariant 10 — no new motion). You'll notice the strip visibly slimmed. If 8 feels too thin day-to-day, bump `"thickness"` in the JSON rather than reverting the code.
- [ ] **Hot-reload suppresses our own saves with a 2-second time mute, not content comparison.** Consequence: a hand-edit you make within ~2 s of an in-app write (e.g. right after Ctrl+Alt+N registers a new terminal) is silently missed until the next change. Content comparison would close that hole but means re-serializing on every event since the registry records don't deep-compare. I judged the race too rare to pay for. Confirm, or tell me to do content hashing instead.

### 🟡 Human read / acceptance work (do this next)

- [ ] **First M4 check:** right-click the strip → Settings…, then rebind "Open the overlay
  switcher" off ``Ctrl+` `` (your VS Code conflict from `docs/private/JPC-TODO.md`). Click the
  chord box and press the keys; it should take effect with no restart. Then run the four M4
  slice checklists in `docs/PROGRESS.md`.
- [ ] Skim ADR-0018 — it encodes the four M4 decisions above.

- [ ] **First M3 check, before anything else:** restart Switchboard on `m3-switcher-picker`, press ``Ctrl+` ``, and confirm the filter box actually accepts typed characters. Windows' foreground lock refusing us focus is the one risk that would sink the whole overlay design; `WindowFocuser` should handle it, but only hardware proves it. If it types, run the four M3 slice checklists in `docs/PROGRESS.md` (they're written step-by-step).
- [ ] Skim ADRs 0015–0017 in `docs/adr/` — cycle-recent snapshot semantics, the overlay activation contract, and the picker/WT-settings decision. They encode the 🔴 items above plus the calls you already made in planning.
- [ ] Still owed from M2 (predates this file's last update): visual check of the stacked rail cards on both edges; Slice 4 motion-status verify incl. the CPU number for ADR-0013's Consequences; Slice 6 rename/recolor popup verify; and bump `railTopHeightDips` 150 → 190 in your live switchboard.json or the top-edge rail will clip.
- [ ] Run the app and shake down the new placement: strip on the configured edge/monitor at correct thickness, then change display scale and unplug/replug a monitor — it should re-dock within ~a second. What to judge: does it ever land on the wrong monitor or flicker?
- [ ] Hot-reload pass: with the app running, hand-edit `%APPDATA%\Switchboard\switchboard.json` — change a color (live frame should update), change `"edge"`/`"monitor"` (strip should move), then deliberately break the JSON (app must keep running, warning in the log). Log dir: `%LOCALAPPDATA%\Switchboard\logs`.
- [ ] Right-click the strip: try all three menu items. On Quit, check tracked WT windows get their default frame color back and the process actually exits.
- [ ] Skim ADR-0007 and ADR-0008 in `docs/adr/` — they encode the two calls above.
- [ ] M1 exit begins now: use it daily and note which real WT titles the default status rules miss (that tuning is the point of M1).

### 🟢 Follow-ups I deliberately deferred (confirm you agree)

- [ ] Hold-to-peek on the switcher → post-M3 (your call in planning: daily use should pick its semantics first; also needs key-up detection we don't have).
- [ ] `NextName` race on rapid quick-launches (two launches before the first registers could collide, breaking title-binding) → pre-existing, picker naturally serializes; noted, not fixed.
- [ ] Active-segment thickness growth → M2 (would be new motion; glow already marks active).
- [x] Surfacing stolen hotkeys to you (currently a log warning only) → *(Shipped in M4: the Hotkeys page badges every chord Bound / taken by another app / unparseable.)*
- [ ] `.bak` recovery when switchboard.json is corrupt at startup → you chose the mid-size scope; startup still fails loudly.
- [x] Last-used-directory memory for quick launch → M3 per PRD R21. *(Shipped this session as `quickLaunch.lastDirectory`.)*

### ✅ Done this session (for your memory)

- M3 planned (you approved: WT settings.json profiles, registry-order grid + MRU preselect, peek deferred) and implemented end-to-end.
- Cycle-recent now walks a real MRU with frozen-snapshot sessions (ADR-0015); the old A↔B toggle is gone.
- ``Ctrl+` `` overlay switcher: live-thumbnail card grid, fuzzy filter with highlighting, arrows/1–9/Enter/Esc, MRU preselect (ADR-0016).
- `Ctrl+Alt+N` picker: Tab cycles profiles read live from WT's settings.json, keep toggle, directory memory; `Ctrl+Alt+Shift+N` keeps the zero-UI launch (ADR-0017).
- 88 → 108 tests; build/format clean; three commits on `m3-switcher-picker`.
- CLAUDE.md staleness fixed (`RegisterAll` → `Rebind`, milestone section); PRD/ROADMAP/PROGRESS/example JSON updated.

### Open questions worth a later think (no action now)

- `persistByDefault` is `true`, so every Ctrl+Alt+N terminal accumulates in the registry forever and the throwaway-cleanup path (PRD R8) never runs. Is that the default you actually want?
- On Windows 10 the frame colorizer is a silent no-op (DWM border color is Win11+) — only `--tabColor` distinguishes windows. Acceptable floor?
- Strip rebuilds restart a mid-pulse attention animation. Harmless in theory; see if it bothers you in practice.

### Session recap (last 5 sessions)

#### 2026-09-04 — First Windows build + M1 daily-driver slice
- First successful Windows build: TFM/platform fix, P/Invoke leak, XAML trigger; 34 → 51 tests.
- Per-monitor DPI-correct strip placement; re-docks on DPI/display changes.
- switchboard.json hot-reload end-to-end incl. live hotkey rebinds.
- Strip context menu, single-instance guard, file logging + crash capture.
- New WT windows focus on launch; startup glow and closed-terminal strip fixes.
