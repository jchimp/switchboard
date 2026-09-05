using System.Globalization;
using Microsoft.Extensions.Logging;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>
/// Paints a terminal's color onto its window frame with <c>DWMWA_BORDER_COLOR</c>.
/// Windows 11 only; on older builds every call is a silent no-op.
/// </summary>
public sealed class FrameColorizer
{
    private readonly ILogger<FrameColorizer> _log;

    public FrameColorizer(ILogger<FrameColorizer> log)
    {
        _log = log;
    }

    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    /// <summary>Applies the color, dimmed when the window is not focused.</summary>
    public void Apply(nint hwnd, string hexColor, bool focused, double unfocusedFactor = 0.6)
    {
        if (!IsSupported || hwnd == 0)
        {
            return;
        }

        var (r, g, b) = ParseHex(hexColor);
        if (!focused)
        {
            r = (byte)(r * unfocusedFactor);
            g = (byte)(g * unfocusedFactor);
            b = (byte)(b * unfocusedFactor);
        }

        // COLORREF is 0x00BBGGRR.
        uint colorRef = (uint)(r | (g << 8) | (b << 16));
        var hr = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref colorRef, sizeof(uint));
        if (hr != 0)
        {
            _log.LogDebug("DwmSetWindowAttribute(BORDER_COLOR) failed for {Hwnd:X}: 0x{Hr:X8}", hwnd, hr);
        }
    }

    public void Reset(nint hwnd)
    {
        if (!IsSupported || hwnd == 0)
        {
            return;
        }

        uint value = NativeMethods.DWMWA_COLOR_DEFAULT;
        _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref value, sizeof(uint));
    }

    internal static (byte R, byte G, byte B) ParseHex(string hex)
    {
        var span = hex.AsSpan().TrimStart('#');
        if (span.Length != 6)
        {
            throw new FormatException($"Expected #RRGGBB, got '{hex}'.");
        }

        return (
            byte.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
