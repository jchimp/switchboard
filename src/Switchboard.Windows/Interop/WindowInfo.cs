using System.Text;

namespace Switchboard.Windows.Interop;

/// <summary>Small, allocation-light helpers over the raw imports.</summary>
internal static class WindowInfo
{
    /// <summary>Top-level window class Windows Terminal uses for every window.</summary>
    public const string TerminalWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";

    public static string GetTitle(nint hwnd)
    {
        var sb = new StringBuilder(512);
        _ = NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetClassName(nint hwnd)
    {
        var sb = new StringBuilder(256);
        _ = NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool IsTerminalWindow(nint hwnd) =>
        hwnd != 0 && NativeMethods.IsWindow(hwnd) && GetClassName(hwnd) == TerminalWindowClass;

    public static IReadOnlyList<nint> EnumerateTerminalWindows()
    {
        var found = new List<nint>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (NativeMethods.IsWindowVisible(hwnd) && IsTerminalWindow(hwnd))
            {
                found.Add(hwnd);
            }

            return true;
        }, 0);
        return found;
    }
}
