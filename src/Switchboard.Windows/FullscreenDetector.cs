using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>
/// Answers "is a fullscreen window covering the strip's monitor?" for the non-app-bar path
/// of hide-over-fullscreen (ADR-0011). App bars get told via ABN_FULLSCREENAPP instead.
/// Called on foreground changes only — event-driven, never polled.
/// </summary>
public static class FullscreenDetector
{
    // The desktop's own windows span the whole monitor but are not "a fullscreen app".
    private static readonly string[] DesktopClasses = ["Progman", "WorkerW"];

    public static bool IsForegroundFullscreen(nint stripHwnd)
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == 0 || fg == stripHwnd || !NativeMethods.IsWindow(fg))
        {
            return false;
        }

        if (DesktopClasses.Contains(WindowInfo.GetClassName(fg), StringComparer.Ordinal))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(fg, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == 0 ||
            monitor != NativeMethods.MonitorFromWindow(stripHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST))
        {
            return false; // fullscreen elsewhere doesn't touch this strip
        }

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>(),
        };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info) || !NativeMethods.GetWindowRect(fg, out var rect))
        {
            return false;
        }

        return rect.Left <= info.rcMonitor.Left && rect.Top <= info.rcMonitor.Top
            && rect.Right >= info.rcMonitor.Right && rect.Bottom >= info.rcMonitor.Bottom;
    }
}
