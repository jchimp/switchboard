using Switchboard.Core.Screen;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>Moves a window to an exact physical-pixel rect without activating it (ADR-0007, invariant 7).</summary>
public static class WindowPlacer
{
    public static void MoveWithoutActivating(nint hwnd, PixelRect rect) =>
        NativeMethods.SetWindowPos(hwnd, 0, rect.X, rect.Y, rect.Width, rect.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOOWNERZORDER);

    /// <summary>The display-change messages the strip re-docks on.</summary>
    public static bool IsDisplayChangeMessage(int msg) =>
        msg is NativeMethods.WM_DISPLAYCHANGE or NativeMethods.WM_SETTINGCHANGE;
}
