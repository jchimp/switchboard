# ADR-0007: Strip placement is computed in Core and applied in physical pixels

**Status:** Accepted · 2026-09-04

## Context
`StripWindow.Dock()` used `SystemParameters.WorkArea`, which only describes the primary monitor and is expressed in DIPs scaled by the *primary* monitor's DPI. The app manifest declares PerMonitorV2, so on a mixed-DPI setup the DIP math is wrong the moment the strip targets a secondary monitor, and `StripSettings.Monitor` was never read at all. Setting WPF `Left/Top/Width/Height` across monitors with different scales is a two-pass converge-with-flicker dance because WPF reinterprets those DIPs against the window's *current* monitor.

## Decision
- Placement math lives in Core (`StripPlacement`): pure functions over platform-neutral `MonitorBounds` snapshots (work area in physical pixels + DPI scale + primary flag), unit-tested cross-platform.
- Monitor enumeration (`MonitorLayout`) and the move itself (`WindowPlacer`) are thin wrappers in Switchboard.Windows over `EnumDisplayMonitors`/`GetMonitorInfo`/`GetDpiForMonitor` and `SetWindowPos`.
- The strip is positioned with `SetWindowPos` in **physical pixels** with `SWP_NOACTIVATE` — single-pass and deterministic; WPF absorbs the resulting `WM_DPICHANGED`/`WM_SIZE` and rescales content itself. `Thickness` stays documented as DIPs; physical thickness is `round(Thickness × dpi/96)` of the *target* monitor.
- `monitor` index semantics: **0 = primary; 1..N = non-primary monitors ordered by virtual-screen position (left, then top)**. An out-of-range index falls back to primary. Primary-first ordering keeps `0` stable regardless of how Windows enumerates.
- Re-dock on `OnDpiChanged`, and on `WM_DISPLAYCHANGE`/`WM_SETTINGCHANGE` behind a short debounce (display changes fire in bursts).

## Consequences
- The strip lands pixel-perfect on any monitor and survives scale changes, resolution changes, and unplugged monitors (falls back to primary).
- WPF `Left/Top/Width/Height` are no longer set for docking; layout follows the actual size delivered by `WM_SIZE`.
- If zero monitors are reported (session transitions, RDP), the strip keeps its last rect until the next display change.
