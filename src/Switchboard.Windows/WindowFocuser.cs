using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>Brings a terminal window to the front, restoring it if minimized.</summary>
public static class WindowFocuser
{
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
