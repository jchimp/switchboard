using Microsoft.Extensions.Logging;
using Switchboard.Core.Screen;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>
/// One DWM live-thumbnail registration: composites <c>source</c>'s current frame into a
/// sub-rectangle of <c>destination</c>'s client area. DWM allows many independent thumbnails
/// against the same destination window, each with its own rect — the rail registers one per
/// row against its own HWND, no per-row child windows needed. DWM preserves the source's
/// aspect ratio and letterboxes automatically, so no manual fitting math is required.
/// </summary>
public sealed class WindowThumbnail : IDisposable
{
    private readonly ILogger<WindowThumbnail> _log;
    private nint _thumbnail;

    public WindowThumbnail(ILogger<WindowThumbnail> log)
    {
        _log = log;
    }

    public bool IsRegistered => _thumbnail != 0;

    /// <summary>Registers the relationship. Re-registering (e.g. the source relaunched) replaces any prior one.</summary>
    public bool Register(nint destination, nint source)
    {
        if (IsRegistered)
        {
            Unregister();
        }

        if (destination == 0 || source == 0 || !NativeMethods.IsWindow(source))
        {
            return false;
        }

        var hr = NativeMethods.DwmRegisterThumbnail(destination, source, out var handle);
        if (hr != 0)
        {
            _log.LogDebug("DwmRegisterThumbnail failed for {Source:X}: 0x{Hr:X8}", source, hr);
            return false;
        }

        _thumbnail = handle;
        return true;
    }

    /// <summary><paramref name="rect"/> is relative to the destination window's client area.</summary>
    public void UpdateDestination(PixelRect rect, byte opacity = 255, bool visible = true)
    {
        if (!IsRegistered)
        {
            return;
        }

        var props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = NativeMethods.DWM_TNP_RECTDESTINATION | NativeMethods.DWM_TNP_OPACITY
                    | NativeMethods.DWM_TNP_VISIBLE | NativeMethods.DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = new NativeMethods.RECT
            {
                Left = rect.X,
                Top = rect.Y,
                Right = rect.X + rect.Width,
                Bottom = rect.Y + rect.Height,
            },
            opacity = opacity,
            fVisible = visible,
            fSourceClientAreaOnly = true, // skip the terminal's own title bar in a 76x46 preview
        };

        var hr = NativeMethods.DwmUpdateThumbnailProperties(_thumbnail, ref props);
        if (hr != 0)
        {
            _log.LogDebug("DwmUpdateThumbnailProperties failed: 0x{Hr:X8}", hr);
        }
    }

    public void Unregister()
    {
        if (!IsRegistered)
        {
            return;
        }

        NativeMethods.DwmUnregisterThumbnail(_thumbnail);
        _thumbnail = 0;
    }

    public void Dispose() => Unregister();
}
