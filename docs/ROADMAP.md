# Roadmap

Milestones are vertical slices; each one is usable on its own.

## M0 — Skeleton (this commit)
- Solution, projects, build props, tests, docs, ADRs.
- Core: registry model + store, status engine, wt command builder, hotkey chord parser.
- Windows: DWM colorizer, WinEvent tracker, global hotkeys, focuser, launcher.
- App: composition root, `TerminalSession`, strip window, jump/new/attention hotkeys.
- **Exit:** `dotnet build` and `dotnet test` green on Windows.

## M1 — Strip + colored frames
- Strip renders from the registry, docks to the configured edge, reflects state.
- `Ctrl+Alt+N` opens a colored WT window and a new segment appears.
- `Ctrl+Alt+1…9` jumps; click-to-focus; restart recovery by title.
- Per-monitor DPI and placement via `Screen` APIs (replace `SystemParameters.WorkArea`).
- **Exit:** author runs it daily. First real-world status-rule tuning.

## M2 — Rail with live thumbnails
- Hover/hotkey slides out the rail (vertical for left/right, horizontal for top).
- `DwmRegisterThumbnail` per row; thumbnail-motion status source.
- App-bar mode (`SHAppBarMessage`) toggle; hide over fullscreen.
- Unbound WT windows listed with "adopt as…".
- Rename / recolor inline from the rail.
- Strip auto-hide to a colored sliver; Attention pulses the segment color and slides the strip out (ADR-0010).
- Opt-in `hideFromTaskbar`: remove bound windows' taskbar buttons via `ITaskbarList`, restore on unbind/exit, survive Explorer restarts (ADR-0009).

## M3 — Overlay switcher + quick-launch picker
- `Ctrl+`` overlay: thumbnail grid, fuzzy filter, 1–9, arrows, Enter, Esc (ADR-0016). Hold-to-peek deferred until daily use picks its semantics.
- `Ctrl+Alt+N` picker: Tab cycles WT profiles (read from WT's settings.json), Enter launches, "keep" toggle, last-dir memory; `Ctrl+Alt+Shift+N` quiet launch (ADR-0017).
- Cycle-recent walks an MRU list rather than a single previous (ADR-0015).

## M4 — Settings UI
- The settings/registry screen from `docs/design/settings-registry.html`, minus the mockup's
  Appearance page: terminals table + editor, edge strip, hotkeys, status rules (ADR-0018).
- Live-apply, debounced — no Save buttons outside the terminal editor.
- Hotkey rows show when another application owns a chord, instead of a log-only warning.
- Import from running windows, in bulk.
- Only settings the registry actually has are shown; the named-pipe source moved out (below).

## M5 — Polish & ship
- Named-pipe status source with sample `pwsh` and `bash` hooks (PRD R19 source 4). Deferred
  from M4: it is the only status source needing per-shell setup, and title rules plus
  thumbnail motion already carry the daily case.
- Tray icon (pause strip, open settings, quit). Start with Windows.
- Single-folder `dotnet publish`, winget manifest.
- Multi-monitor: follow-cursor strip, per-monitor override.
- Telemetry-free crash log to `%LOCALAPPDATA%\Switchboard\logs`.

## Parked ideas
- Group terminals (work / personal) with strip sections.
- Per-terminal "sticky note" shown in the rail.
- Remote (SSH) terminals as first-class registry rows.
- Pane-level color via WT `--colorScheme` per profile.
