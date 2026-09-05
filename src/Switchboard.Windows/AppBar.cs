using Microsoft.Extensions.Logging;
using Switchboard.Core.Registry;
using Switchboard.Core.Screen;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>What an app-bar callback message meant, translated for the App layer.</summary>
public enum AppBarNotification
{
    /// <summary>The taskbar or another app bar moved; re-negotiate and re-dock.</summary>
    PositionChanged,
    FullscreenEntered,
    FullscreenExited,
}

/// <summary>
/// Registers the strip's band as a shell app bar so its pixels are reserved desktop space
/// (ADR-0011). Owns the whole SHAppBarMessage lifecycle; callers propose a rect from the
/// full monitor bounds and apply whatever the shell grants. UI thread only.
/// </summary>
public sealed class AppBar : IDisposable
{
    private readonly ILogger<AppBar> _log;
    private nint _hwnd;

    public AppBar(ILogger<AppBar> log)
    {
        _log = log;
        CallbackMessage = NativeMethods.RegisterWindowMessage("SwitchboardAppBarCallback");
    }

    /// <summary>The private message the shell posts app-bar notifications through.</summary>
    public int CallbackMessage { get; }

    public bool IsRegistered => _hwnd != 0;

    /// <summary>
    /// Registers (if needed) and negotiates the reserved band: QUERYPOS lets the shell clip
    /// the proposal against the taskbar and other app bars, then the granted rect is
    /// re-anchored to our thickness and committed with SETPOS. Returns the rect the window
    /// should actually occupy, or null when registration failed.
    /// </summary>
    public PixelRect? Apply(nint hwnd, ScreenEdge edge, PixelRect proposed)
    {
        if (hwnd == 0)
        {
            return null;
        }

        if (_hwnd != 0 && _hwnd != hwnd)
        {
            Remove(); // a new HWND (theme recreation) needs a fresh registration
        }

        if (_hwnd == 0)
        {
            var reg = Data(hwnd);
            reg.uCallbackMessage = (uint)CallbackMessage;
            if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref reg) == 0)
            {
                _log.LogWarning("ABM_NEW failed; screen space will not be reserved");
                return null;
            }

            _hwnd = hwnd;
        }

        var data = Data(hwnd);
        data.uEdge = ToAbe(edge);
        data.rc = ToRect(proposed);
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref data);

        // The shell only pushes the free edge; re-assert our thickness against it.
        var thickness = edge is ScreenEdge.Left or ScreenEdge.Right ? proposed.Width : proposed.Height;
        switch (edge)
        {
            case ScreenEdge.Left:
                data.rc.Right = data.rc.Left + thickness;
                break;
            case ScreenEdge.Right:
                data.rc.Left = data.rc.Right - thickness;
                break;
            default:
                data.rc.Bottom = data.rc.Top + thickness;
                break;
        }

        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref data);
        return new PixelRect(
            data.rc.Left, data.rc.Top,
            data.rc.Right - data.rc.Left, data.rc.Bottom - data.rc.Top);
    }

    /// <summary>Releases the reserved band. Safe to call when not registered.</summary>
    public void Remove()
    {
        if (_hwnd == 0)
        {
            return;
        }

        var data = Data(_hwnd);
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref data);
        _hwnd = 0;
        _log.LogInformation("App bar unregistered");
    }

    /// <summary>Translates a window message, or returns null if it isn't ours.</summary>
    public AppBarNotification? HandleCallback(int msg, nint wParam, nint lParam)
    {
        if (_hwnd == 0 || msg != CallbackMessage)
        {
            return null;
        }

        return (int)wParam switch
        {
            NativeMethods.ABN_POSCHANGED => AppBarNotification.PositionChanged,
            NativeMethods.ABN_FULLSCREENAPP => lParam != 0
                ? AppBarNotification.FullscreenEntered
                : AppBarNotification.FullscreenExited,
            _ => null,
        };
    }

    public void Dispose() => Remove();

    private static NativeMethods.APPBARDATA Data(nint hwnd) => new()
    {
        cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>(),
        hWnd = hwnd,
    };

    private static uint ToAbe(ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => NativeMethods.ABE_LEFT,
        ScreenEdge.Right => NativeMethods.ABE_RIGHT,
        _ => NativeMethods.ABE_TOP,
    };

    private static NativeMethods.RECT ToRect(PixelRect r) => new()
    {
        Left = r.X,
        Top = r.Y,
        Right = r.X + r.Width,
        Bottom = r.Y + r.Height,
    };
}
