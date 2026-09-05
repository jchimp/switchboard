using Switchboard.Core.Screen;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>Snapshots the current monitors for <see cref="StripPlacement"/> (ADR-0007).</summary>
public static class MonitorLayout
{
    public static IReadOnlyList<MonitorBounds> Snapshot()
    {
        var monitors = new List<MonitorBounds>();
        NativeMethods.EnumDisplayMonitors(0, 0, (nint hMonitor, nint _, ref NativeMethods.RECT _, nint _) =>
        {
            var info = new NativeMethods.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            // A DPI query failure (remote sessions, exotic drivers) degrades to 96 dpi, not to no strip.
            var scale = NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0
                ? dpiX / 96.0
                : 1.0;

            monitors.Add(new MonitorBounds
            {
                WorkArea = new PixelRect(
                    info.rcWork.Left, info.rcWork.Top,
                    info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top),
                Bounds = new PixelRect(
                    info.rcMonitor.Left, info.rcMonitor.Top,
                    info.rcMonitor.Right - info.rcMonitor.Left, info.rcMonitor.Bottom - info.rcMonitor.Top),
                DpiScale = scale,
                IsPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
            });
            return true;
        }, 0);

        return monitors;
    }
}
