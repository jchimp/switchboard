# ADR-0001: WPF on .NET 8, not WinUI 3

**Status:** Accepted · 2026-09-04

## Context
Switchboard needs: a borderless, topmost, non-activating window pinned to a screen edge; raw HWND access for `DwmRegisterThumbnail`, `DwmSetWindowAttribute`, `SHAppBarMessage`, `RegisterHotKey`; per-monitor DPI; a message pump hook for `WM_HOTKEY`. It does not need touch, Fluent controls, or Store packaging.

## Decision
WPF on .NET 8 with per-monitor V2 DPI awareness. `HwndSource` gives us the HWND and a `WndProc` hook directly. C# for everything; no C++/CLI.

## Consequences
- Win32 interop is straightforward; the App layer is thin.
- Visuals are hand-styled (the strip/rail/overlay are custom anyway).
- WinUI 3's windowing (AppWindow) would fight the non-activating edge window and the thumbnail host. Revisit only if a Store listing becomes a goal.
