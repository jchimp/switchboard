## JEREMY-TODO — Switchboard review queue

Personal working notes for Jeremy (not for the engine). Skim top-to-bottom.

### 🔴 Decisions I need from you (I made a call; confirm or override)

- [ ] **Monitor index semantics: 0 = primary, 1..N = secondaries ordered left-to-right.** `"monitor"` in switchboard.json was never read before, so I had to define what the number means. I picked primary-first with position ordering so the value survives Windows' arbitrary enumeration order and an unplugged monitor falls back to primary instead of erroring. The alternative is raw enumeration order (what most tools do), which silently remaps when you dock/undock. Written up in ADR-0007. Confirm the semantics, or tell me to use enumeration order.
- [ ] **The strip got thinner: it now renders at `thickness` (8 px) instead of `activeThickness` (14 px).** The old code used the wrong setting; `activeThickness` is reserved for M2's active-segment growth, which I deferred entirely (invariant 10 — no new motion). You'll notice the strip visibly slimmed. If 8 feels too thin day-to-day, bump `"thickness"` in the JSON rather than reverting the code.
- [ ] **Hot-reload suppresses our own saves with a 2-second time mute, not content comparison.** Consequence: a hand-edit you make within ~2 s of an in-app write (e.g. right after Ctrl+Alt+N registers a new terminal) is silently missed until the next change. Content comparison would close that hole but means re-serializing on every event since the registry records don't deep-compare. I judged the race too rare to pay for. Confirm, or tell me to do content hashing instead.

### 🟡 Human read / acceptance work (do this next)

- [ ] Run the app and shake down the new placement: strip on the configured edge/monitor at correct thickness, then change display scale and unplug/replug a monitor — it should re-dock within ~a second. What to judge: does it ever land on the wrong monitor or flicker?
- [ ] Hot-reload pass: with the app running, hand-edit `%APPDATA%\Switchboard\switchboard.json` — change a color (live frame should update), change `"edge"`/`"monitor"` (strip should move), then deliberately break the JSON (app must keep running, warning in the log). Log dir: `%LOCALAPPDATA%\Switchboard\logs`.
- [ ] Right-click the strip: try all three menu items. On Quit, check tracked WT windows get their default frame color back and the process actually exits.
- [ ] Skim ADR-0007 and ADR-0008 in `docs/adr/` — they encode the two calls above.
- [ ] M1 exit begins now: use it daily and note which real WT titles the default status rules miss (that tuning is the point of M1).

### 🟢 Follow-ups I deliberately deferred (confirm you agree)

- [ ] Active-segment thickness growth → M2 (would be new motion; glow already marks active).
- [ ] Surfacing stolen hotkeys to you (currently a log warning only — at least it's in the file log now) → with the M2 rail UI.
- [ ] `.bak` recovery when switchboard.json is corrupt at startup → you chose the mid-size scope; startup still fails loudly.
- [ ] Last-used-directory memory for quick launch → M3 per PRD R21.

### ✅ Done this session (for your memory)

- First successful Windows build ever for this repo: TFM/platform fix, style errors, App-layer P/Invoke leak, invalid XAML trigger. 34 → 51 tests green.
- New WT windows come to the foreground on launch (launch-scoped; recovery never steals focus).
- Per-monitor DPI-correct strip placement, computed in Core (tested), applied via `SetWindowPos`, re-docks on DPI/display changes.
- switchboard.json hot-reload end-to-end, including live hotkey rebinds and status-rule replacement.
- Strip context menu (Quit/Reapply/Reload), single-instance guard, file logging with 7-day retention and crash capture.
- Strip bugs fixed: closed-at-startup terminals now appear when launched; focused terminal glows immediately at startup.

### Open questions worth a later think (no action now)

- `persistByDefault` is `true`, so every Ctrl+Alt+N terminal accumulates in the registry forever and the throwaway-cleanup path (PRD R8) never runs. Is that the default you actually want?
- On Windows 10 the frame colorizer is a silent no-op (DWM border color is Win11+) — only `--tabColor` distinguishes windows. Acceptable floor?
- Strip rebuilds restart a mid-pulse attention animation. Harmless in theory; see if it bothers you in practice.

### Session recap (last 5 sessions)
