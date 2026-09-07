using Switchboard.Core.Screen;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>Moves a window to an exact physical-pixel rect without activating it (ADR-0007, invariant 7).</summary>
public static class WindowPlacer
{
    public static void MoveWithoutActivating(nint hwnd, PixelRect rect) =>
        NativeMethods.SetWindowPos(hwnd, 0, rect.X, rect.Y, rect.Width, rect.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOOWNERZORDER);

    /// <summary>
    /// Position-only move. Skipping the size lets a layered (AllowsTransparency) window
    /// slide without re-entering the surface-reallocation path every frame — the rail's
    /// open/close rects are the same size, so its slide is exactly this.
    /// </summary>
    public static void MoveWithoutResizing(nint hwnd, int x, int y) =>
        NativeMethods.SetWindowPos(hwnd, 0, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOOWNERZORDER);

    /// <summary>The display-change messages the strip re-docks on.</summary>
    public static bool IsDisplayChangeMessage(int msg) =>
        msg is NativeMethods.WM_DISPLAYCHANGE or NativeMethods.WM_SETTINGCHANGE;
}
