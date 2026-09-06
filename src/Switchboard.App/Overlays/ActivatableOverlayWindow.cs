using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Switchboard.Core.Screen;
using Switchboard.Windows;

namespace Switchboard.App.Overlays;

/// <summary>
/// Base for the product's activatable overlays — the switcher and the quick-launch picker
/// (ADR-0016). Unlike the strip and rail these take real keyboard focus, so the contract is
/// a round trip: capture the foreground window on open, hand focus back on Esc/cancel, and
/// deliberately keep it moved when the overlay itself focused a terminal. Click-away
/// (Deactivated) hides without touching focus — the user already put it where they want it.
/// Hidden, not closed, between uses.
/// </summary>
public abstract class ActivatableOverlayWindow : Window
{
    private nint _restoreTo;

    protected ActivatableOverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Deactivated += (_, _) =>
        {
            if (IsVisible)
            {
                Hide();
            }
        };
    }

    public bool IsOpen => IsVisible;

    /// <summary>Shows the overlay and takes focus. No-op when already open.</summary>
    protected void OpenOverlay()
    {
        if (IsVisible)
        {
            return;
        }

        _restoreTo = WindowFocuser.CurrentForeground();
        var helper = new WindowInteropHelper(this);
        if (helper.Handle == 0)
        {
            helper.EnsureHandle();
        }

        OnOpening();
        Show();

        // Activate() from a background process can be refused by the foreground lock; the
        // focuser already owns the AttachThreadInput fallback dance.
        WindowFocuser.Focus(helper.Handle);
        OnOpened();
    }

    /// <summary>Runs before Show(), with the HWND created — seed state, compute placement.</summary>
    protected virtual void OnOpening()
    {
    }

    /// <summary>Runs after the overlay has taken focus — move keyboard focus to an input.</summary>
    protected virtual void OnOpened()
    {
    }

    /// <summary>
    /// Centers on the given monitor's work area. Call from a Loaded-priority dispatch —
    /// SizeToContent means the actual size exists only after the first layout pass.
    /// </summary>
    protected void CenterOnMonitor(int monitorIndex)
    {
        var monitors = MonitorLayout.Snapshot();
        if (monitors.Count == 0)
        {
            return;
        }

        var work = StripPlacement.SelectMonitor(monitors, monitorIndex).WorkArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = (work.X + (work.Width - ActualWidth * dpi.DpiScaleX) / 2) / dpi.DpiScaleX;
        Top = (work.Y + (work.Height - ActualHeight * dpi.DpiScaleY) / 2) / dpi.DpiScaleY;
    }

    /// <summary>Esc / cancel: the round trip ends where it started.</summary>
    public void CloseRestoringFocus()
    {
        Hide();
        WindowFocuser.Focus(_restoreTo);
    }

    /// <summary>Commit: the overlay just focused (or launched) a terminal; leave focus there.</summary>
    public void CloseWithoutRestore() => Hide();

    protected override void OnClosing(CancelEventArgs e)
    {
        // The overlay is a reusable singleton: Alt+F4 must hide it, not destroy the HWND a
        // later Show() would need. Real shutdown still tears it down.
        if (Application.Current?.Dispatcher.HasShutdownStarted != true)
        {
            e.Cancel = true;
            CloseRestoringFocus();
        }

        base.OnClosing(e);
    }
}
