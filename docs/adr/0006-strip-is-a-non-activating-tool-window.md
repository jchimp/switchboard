# ADR-0006: The strip is a non-activating tool window, top edge by default

**Status:** Accepted · 2026-09-04

## Context
The strip must be visible at all times, never take focus, never appear in Alt-Tab or the taskbar, and must not collide with a left-docked editor sidebar or Windows 11 snap layouts.

## Decision
- `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, `Topmost`, `ShowInTaskbar=false`, `ShowActivated=false`.
- Default edge is **top**: the rail drops down as a horizontal row of wide thumbnails (closer to real terminal proportions), and it sidesteps left-edge collisions. Left/right remain supported.
- App-bar mode (`SHAppBarMessage`) is an option, off by default; recommended on ultrawides.
- Hidden automatically over fullscreen windows (M2).

## Consequences
- With app-bar mode off, a maximized window's title bar overlaps the strip's pixels; the strip stays on top, so 8 px of title bar are covered. Users who mind turn app-bar mode on.
