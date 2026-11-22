# ADR-0018: The settings window is a normal application window

**Status:** Accepted · 2026-09-08 · Extends ADR-0016, amends invariant 7, implements ROADMAP M4

## Context
Every setting has been hand-edited in `switchboard.json` by design — CLAUDE.md's "don't add
a settings UI before M4" kept the registry the single, obvious surface while the product
shape moved. M4 lifts that rule, and the mockup in `docs/design/settings-registry.html` is
the target.

Three of the product's windows are deliberately strange. The strip and rail never activate
(invariant 7); the overlays activate but hide themselves the moment they lose focus, restore
the foreground you came from, and never appear in the taskbar (ADR-0012, ADR-0014, ADR-0016).
All three exist to be glanced at, not worked in. A settings screen is the opposite: you open
it, you stay in it for a minute, you may Alt-Tab to a terminal to check something and come
back. Every overlay behaviour above is actively wrong for it — `Deactivated`-hides would
destroy the window the moment you clicked another app.

The mockup also draws controls the registry has no field for (title prefix, dim-frame-when-
unfocused, tab-color toggle, rail thumbnail size, theme, "hide strip 30 min", "launch all" /
"launch missing"), and it draws status-source toggles for taskbar flash and the named pipe.
`StatusEngine.Flashed` has no caller and the pipe is not implemented.

## Decision
- **The settings window is an ordinary WPF application window** — resizable, present in the
  taskbar and Alt-Tab, closed by its own chrome. It is the fourth activatable window and the
  first one that is not an overlay: it does **not** capture or restore the previous
  foreground, and it does **not** hide on `Deactivated`. It is a place you go, not something
  that flashes over your work.
- **Native chrome, dark title bar.** Rather than reimplementing the mockup's custom titlebar,
  the window keeps the system frame (free resize, snap, system menu) and sets
  `DWMWA_USE_IMMERSIVE_DARK_MODE` so the caption matches the product's dark surfaces. That is
  one new `DwmSetWindowAttribute` use in `NativeMethods.cs` behind a typed wrapper
  (invariant 2), the `FrameColorizer` pattern.
- **Live-apply, debounced — no Save buttons on the settings pages.** Edits are coalesced on a
  400 ms timer and applied through `TerminalSession`, which persists via the existing
  `PersistAsync` (atomic write, self-write mute) and fires `RegistryChanged`. This makes the
  UI exactly equivalent to a hand-edit landing through hot-reload, so there is one apply path
  in the product rather than two. The **terminal editor keeps an explicit Save**: a name
  change rebinds a live window (ADR-0014), which is not something to do on every keystroke.
- **The UI shows only settings the registry actually has.** Controls with no backing field
  are not drawn, and the mockup's Appearance page — every control on which is unbacked — is
  not built. Four nav items, not five. `strip.activeThickness` is likewise hidden: it is
  reserved and unread.
- **Dead status sources are not drawn either.** No taskbar-flash toggle (`Flashed` has no
  caller) and no named-pipe toggle. The pipe leaves M4 entirely; PRD R19 and the roadmap are
  amended to say "later, opt-in" rather than M4.
- The window is a DI singleton like the other windows, hidden rather than disposed on close so
  its view models keep their `RegistryChanged` subscriptions. Opened from the "Settings…" item
  on the strip and rail context menus.

## Consequences
- Invariant 7 now reads: the strip and rail never activate; `RailEditPopup`,
  `ActivatableOverlayWindow` descendants, and `SettingsWindow` are the sanctioned exceptions —
  and `SettingsWindow` is the only one that behaves like a normal app.
- Live-apply means `RegistryChanged` fires far more often than before — a slider drag is one
  save per debounce tick, each rebuilding the strip and rebinding hotkeys. Cheap at this size,
  but it is why the settings view models must ignore the `RegistryChanged` raised by their own
  save; otherwise the round trip overwrites the field being typed in.
- ADR-0008's ~2 s self-write mute now covers a much busier writer. A hand-edit made within
  ~2 s of a settings change is still silently missed. Accepted, unchanged, and worth knowing.
- Numeric settings reachable by slider can make the strip unusable (thickness 0, hidden
  thickness above thickness). Pure `SettingsBounds` clamps in Core rather than trusting the
  controls' own ranges, because the file is still hand-editable.
- Dropping the Appearance page means the mockup and the shipped product visibly differ. The
  mockup stays as the design record; this ADR is the reason for the gap.
- `DWMWA_USE_IMMERSIVE_DARK_MODE` is a no-op on pre-Win11 builds: the window gets a light
  caption and works normally. The same floor the frame colorizer already accepts.
