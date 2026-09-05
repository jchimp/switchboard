# ADR-0011: App-bar mode reserves the strip's pixels; fullscreen hides it

**Status:** Accepted · 2026-09-04 · Extends ADR-0006, ADR-0007, ADR-0010

## Context
ADR-0006 promised an optional app-bar mode (`reserveScreenSpace`, PRD R14): the strip's band
becomes reserved desktop space so maximized windows stop at its edge instead of sliding under
it. ADR-0006 also promised the strip hides over fullscreen windows (`hideOverFullscreen`) —
a strip over a fullscreen video or game is exactly the kind of intrusion the product forbids.

Two mechanics complicate this:

1. **Work-area feedback.** A registered app bar shrinks the monitor's work area by its own
   band. `StripPlacement.Compute` positions from the work area (ADR-0007), so re-docking an
   app-bar strip from the *post-registration* work area would walk it inward one thickness
   per re-dock. App-bar placement must be proposed from the monitor's full bounds and then
   negotiated with the shell (`ABM_QUERYPOS`/`ABM_SETPOS`), which clips against the taskbar
   and other app bars but not against our own registration.
2. **Fullscreen detection has two delivery paths.** A registered app bar is told directly
   (`ABN_FULLSCREENAPP`). An unregistered strip gets no such courtesy and must infer it.

## Decision
- `MonitorBounds` gains the full monitor rect (`Bounds`) beside the work area. A new pure
  `StripPlacement.ComputeAppBar` anchors the proposed band to the full bounds; Core still
  computes, the Windows layer still negotiates and applies (ADR-0007 division holds).
- New typed wrapper `AppBar` (the `TaskbarVisibility` pattern: sealed, disposable, logged,
  degrade-don't-throw) owns the `APPBARDATA` lifecycle: `ABM_NEW` with a registered callback
  message, `ABM_QUERYPOS` → thickness re-anchor → `ABM_SETPOS` on every dock, `ABM_REMOVE`
  on teardown. `ABN_POSCHANGED` funnels into the strip's existing debounced re-dock.
- Fullscreen suppression is dual-path, both gated on `hideOverFullscreen`:
  - app-bar mode: `ABN_FULLSCREENAPP` (lParam says entered/left);
  - otherwise: on every foreground change (the tracker's existing WinEvent — no new hook,
    no polling), `FullscreenDetector` checks whether the foreground window covers the full
    monitor rect *of the strip's own monitor*. The desktop (`Progman`/`WorkerW`) is excluded
    so an empty desktop doesn't read as fullscreen.
  - Suppression hides the WPF window (`Visibility`), keeping the HWND, hooks, and any
    app-bar registration alive.
- Precedence stays as ADR-0010 set it: `reserveScreenSpace` disables auto-hide
  (`AutoHideActive` already encodes this); in app-bar mode the strip is always full
  thickness.

## Consequences
- Toggling `reserveScreenSpace` live (registry hot-reload, ADR-0008) registers/unregisters
  the app bar on the next dock; maximized windows re-flow immediately.
- The reserved band persists while the strip is fullscreen-hidden — same behavior as the
  taskbar, and fullscreen windows cover app bars anyway; unregistering per fullscreen event
  would churn every maximized window's layout.
- Fullscreen inference on the non-app-bar path runs only on foreground changes (event-driven,
  invariant: no polling) and costs three Win32 calls per switch.
- A crash can strand a reserved band until the shell notices the dead HWND; `AppBar.Dispose`
  and window close both remove it on any orderly exit.
