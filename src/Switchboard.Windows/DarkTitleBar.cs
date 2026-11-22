using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>
/// Paints a window's caption dark with <c>DWMWA_USE_IMMERSIVE_DARK_MODE</c>, so the one
/// window with system chrome (settings, ADR-0018) matches the product's dark surfaces.
/// A silent no-op on builds that predate the attribute.
/// </summary>
public static class DarkTitleBar
{
    /// <summary>Windows 10 2004 (build 19041) is where the attribute settled on 20.</summary>
    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    /// <summary>Call after the HWND exists (WPF: <c>SourceInitialized</c>).</summary>
    public static void Apply(nint hwnd)
    {
        if (!IsSupported || hwnd == 0)
        {
            return;
        }

        uint enabled = 1;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(uint));
    }
}
