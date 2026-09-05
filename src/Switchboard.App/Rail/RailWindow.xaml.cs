using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Switchboard.App.ViewModels;
using Switchboard.Core.Screen;
using Switchboard.Windows;

namespace Switchboard.App.Rail;

/// <summary>
/// The slide-out rail: a second non-activating, topmost tool window adjacent to the strip
/// (ADR-0012). Hidden, not closed, between uses. Opens on strip hover or the toggleRail
/// chord; closes when the mouse leaves it or on the chord again. Never takes focus.
/// </summary>
public partial class RailWindow : Window
{
    private const int SlideDurationMs = 220;

    private readonly RailViewModel _vm;
    private readonly System.Windows.Threading.DispatcherTimer _slideTimer;
    private readonly System.Windows.Threading.DispatcherTimer _hoverProbe;
    private PixelRect _slideFrom;
    private PixelRect _slideTo;
    private long _slideStartTicks;
    private bool _hidingAfterSlide;
    private bool _tracksHover;

    public RailWindow(RailViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        SourceInitialized += (_, _) =>
        {
            // Same contract as the strip: invisible to Alt-Tab, never steals focus (ADR-0012).
            WindowStyler.MakeNonActivatingToolWindow(new WindowInteropHelper(this).Handle);
        };

        _slideTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _slideTimer.Tick += (_, _) => SlideStep();

        // Closing is level-triggered, not edge-triggered: the rail slides under the cursor
        // while opening, so raw MouseLeave fires spuriously mid-slide (the hover bounce).
        // While open, this probe closes the rail only when the cursor is over neither the
        // rail nor the strip. Runs only while the rail is open — user-initiated, transient.
        _hoverProbe = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _hoverProbe.Tick += (_, _) =>
        {
            if (!IsOpen)
            {
                _hoverProbe.Stop();
                return;
            }

            if (_tracksHover && !IsMouseOver && AnchorHovered?.Invoke() != true)
            {
                CloseRail();
            }
        };
    }

    public bool IsOpen { get; private set; }

    /// <summary>The strip listens: while the rail is open the auto-hidden strip stays out.</summary>
    public event Action<bool>? OpenChanged;

    /// <summary>Set by the strip: "is the cursor over me?" — the rail's anchor counts as inside.</summary>
    public Func<bool>? AnchorHovered { get; set; }

    public void Toggle()
    {
        if (IsOpen)
        {
            CloseRail();
        }
        else
        {
            // Hotkey open: don't hover-track until the mouse actually visits the rail,
            // or it would close 200 ms later with the cursor wherever the user left it.
            OpenRail(trackHover: false);
        }
    }

    public void OpenRail(bool trackHover = true)
    {
        if (IsOpen)
        {
            return;
        }

        var monitors = MonitorLayout.Snapshot();
        if (monitors.Count == 0)
        {
            return;
        }

        var s = _vm.Settings;
        var vertical = RailPlacement.IsVertical(s.Edge);
        var size = vertical ? s.RailSizeDips : s.RailTopHeightDips;
        var monitor = StripPlacement.SelectMonitor(monitors, s.Monitor);
        var open = RailPlacement.Compute(monitor, s.Edge, s.Thickness, size);
        var hidden = RailPlacement.ComputeHidden(monitor, s.Edge, s.Thickness, size);

        if (FindPanel() is { } panel)
        {
            panel.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
            ListScroller.VerticalScrollBarVisibility = vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            ListScroller.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        }

        _vm.Sync(); // events keep it fresh, but a cheap resync on open is bulletproof
        _hidingAfterSlide = false;
        _tracksHover = trackHover;
        IsOpen = true;
        _hoverProbe.Start();

        // Create the HWND (fires SourceInitialized → non-activating styles) and park it at
        // the hidden rect before the window is ever visible, so the first open can't flash.
        var helper = new WindowInteropHelper(this);
        if (helper.Handle == 0)
        {
            helper.EnsureHandle();
        }

        WindowPlacer.MoveWithoutActivating(helper.Handle, hidden);
        Show();
        WindowPlacer.MoveWithoutActivating(helper.Handle, hidden);
        Slide(hidden, open);
        OpenChanged?.Invoke(true);
    }

    public void CloseRail()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _hoverProbe.Stop();
        _hidingAfterSlide = true;
        Slide(_slideTo, ReverseTarget());
        OpenChanged?.Invoke(false);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _tracksHover = true; // a hotkey-opened rail becomes hover-tracked once visited
    }

    private PixelRect ReverseTarget()
    {
        var monitors = MonitorLayout.Snapshot();
        var s = _vm.Settings;
        if (monitors.Count == 0)
        {
            return _slideFrom;
        }

        var vertical = RailPlacement.IsVertical(s.Edge);
        var size = vertical ? s.RailSizeDips : s.RailTopHeightDips;
        return RailPlacement.ComputeHidden(StripPlacement.SelectMonitor(monitors, s.Monitor), s.Edge, s.Thickness, size);
    }

    private void Slide(PixelRect from, PixelRect to)
    {
        _slideFrom = from;
        _slideTo = to;
        _slideStartTicks = Environment.TickCount64;
        _slideTimer.Start();
    }

    private void SlideStep()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
        {
            _slideTimer.Stop();
            return;
        }

        var t = Math.Clamp((Environment.TickCount64 - _slideStartTicks) / (double)SlideDurationMs, 0.0, 1.0);
        var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic, same feel as the strip
        WindowPlacer.MoveWithoutActivating(hwnd, new PixelRect(
            Lerp(_slideFrom.X, _slideTo.X, eased),
            Lerp(_slideFrom.Y, _slideTo.Y, eased),
            Lerp(_slideFrom.Width, _slideTo.Width, eased),
            Lerp(_slideFrom.Height, _slideTo.Height, eased)));

        if (t >= 1.0)
        {
            _slideTimer.Stop();
            if (_hidingAfterSlide)
            {
                _hidingAfterSlide = false;
                Hide();
            }
        }
    }

    private static int Lerp(int from, int to, double t) => from + (int)Math.Round((to - from) * t);

    private StackPanel? FindPanel() => FindVisualChild<StackPanel>(Rows);

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T hit)
            {
                return hit;
            }

            if (FindVisualChild<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private async void Row_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RailItemViewModel item })
        {
            CloseRail();
            await _vm.ActivateCommand.ExecuteAsync(item);
        }
    }

    private async void NewTerminal_Click(object sender, MouseButtonEventArgs e)
    {
        CloseRail();
        await _vm.NewTerminalCommand.ExecuteAsync(null);
    }
}
