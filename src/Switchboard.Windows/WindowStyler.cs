using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>Applies the extended window styles the strip needs (invariant 7: it never activates).</summary>
public static class WindowStyler
{
    /// <summary>
    /// Marks the window as a non-activating tool window: invisible to Alt-Tab and
    /// never able to steal focus from a terminal.
    /// </summary>
    public static void MakeNonActivatingToolWindow(nint hwnd)
    {
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, ex | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
    }
}
