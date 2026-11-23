using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>Brings a terminal window to the front, restoring it if minimized.</summary>
public static class WindowFocuser
{
    /// <summary>The current foreground window; the edit popup restores this on close (ADR-0014).</summary>
    public static nint CurrentForeground() => NativeMethods.GetForegroundWindow();

    /// <summary>
    /// True for windows that must never hold focus: the strip and rail (ADR-0006/0012). A menu
    /// closing over them hands focus back instead of leaving it there.
    /// </summary>
    public static bool IsNonActivating(nint hwnd) =>
        hwnd != 0 && (NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_NOACTIVATE) != 0;

    /// <summary>True when <paramref name="hwnd"/> is the foreground window right now.</summary>
    public static bool IsForeground(nint hwnd) => hwnd != 0 && NativeMethods.GetForegroundWindow() == hwnd;

    /// <summary>Minimizes the window; the click-again-to-minimize path (ADR-0019).</summary>
    public static bool Minimize(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return false;
        }

        return NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
    }

    public static bool Focus(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return false;
        }

        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }

        if (NativeMethods.SetForegroundWindow(hwnd))
        {
            return true;
        }

        // Foreground lock: attach to the current foreground thread so Windows lets us steal focus.
        // Legitimate here because the user just pressed a hotkey or clicked the strip.
        var fg = NativeMethods.GetForegroundWindow();
        var fgThread = NativeMethods.GetWindowThreadProcessId(fg, out _);
        var me = NativeMethods.GetCurrentThreadId();
        if (fgThread == me || fgThread == 0)
        {
            return false;
        }

        NativeMethods.AttachThreadInput(me, fgThread, true);
        try
        {
            return NativeMethods.SetForegroundWindow(hwnd);
        }
        finally
        {
            NativeMethods.AttachThreadInput(me, fgThread, false);
        }
    }
}
