using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using Switchboard.App.Rail;
using Switchboard.App.Services;
using Switchboard.App.ViewModels;
using Switchboard.Core.Screen;
using Switchboard.Windows;

namespace Switchboard.App.Strip;

/// <summary>
/// The always-visible edge strip. A borderless, non-activating, topmost window pinned
/// to one screen edge; each terminal is one colored segment. With auto-hide on it
/// collapses to a sliver and slides back out on hover or Attention (ADR-0010).
/// </summary>
public partial class StripWindow : Window
{
    private const int SlideDurationMs = 150;

    private readonly StripViewModel _vm;
    private readonly TaskbarVisibility _taskbarVisibility;
    private readonly AppBar _appBar;
    private readonly RailWindow _rail;
    private readonly System.Windows.Threading.DispatcherTimer _redockDebounce;
    private readonly System.Windows.Threading.DispatcherTimer _collapseTimer;
    private readonly WindowSlide _slide;
    private readonly System.Windows.Threading.DispatcherTimer _hoverOpenTimer;
    private bool _collapsed;
    private bool _fullscreenHidden;
    private PixelRect? _currentRect;

    public StripWindow(
        StripViewModel vm, TaskbarVisibility taskbarVisibility, AppBar appBar,
        TerminalSession session, RailWindow rail)
    {
        _vm = vm;
        _taskbarVisibility = taskbarVisibility;
        _appBar = appBar;
        _rail = rail;

        // Fullscreen detection outside app-bar mode rides the tracker's existing foreground
        // WinEvent (via the session) — no new hook, no polling (ADR-0011).
        session.ForegroundChanged += _ => CheckForegroundFullscreen();

        InitializeComponent();
        DataContext = vm;
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            Dock();
            if (AutoHideActive)
            {
                RestartCollapseTimer(); // collapse shortly after startup even if never hovered
            }
        };
        vm.Items.CollectionChanged += (_, _) => Dock();
        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Display changes arrive in bursts (one WM_SETTINGCHANGE per work-area tweak);
        // coalesce them into a single re-dock.
        _redockDebounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _redockDebounce.Tick += (_, _) =>
        {
            _redockDebounce.Stop();
            Dock();
        };

        _collapseTimer = new System.Windows.Threading.DispatcherTimer();
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (AutoHideActive && !IsMouseOver && !_vm.AnyAttention)
            {
                SetCollapsed(true); // collapsing under an open rail is fine — it covers us
            }
        };

        _hoverOpenTimer = new System.Windows.Threading.DispatcherTimer();
        _hoverOpenTimer.Tick += (_, _) =>
        {
            _hoverOpenTimer.Stop();
            if (IsMouseOver && _vm.Settings.OpenRailOnHover)
            {
                _rail.OpenRail(); // R15: hovering opens the rail
            }
        };

        // Frame-synced slide (CompositionTarget.Rendering) — a Background-priority timer
        // jitters whenever layout or render work competes with it. The strip's slide
        // changes thickness, so each frame both moves and resizes; _currentRect tracks
        // every applied frame so a mid-slide re-dock animates from the true position.
        _slide = new WindowSlide(
            () => new WindowInteropHelper(this).Handle, SlideDurationMs,
            applied: rect => _currentRect = rect);

        // The strip counts as "inside" for the rail's close probe: hovering the strip with
        // the rail open must not close it (and the open slide passes under the cursor).
        rail.AnchorHovered = () => IsMouseOver;

        // The rail covers the strip from the screen edge while open (ADR-0012 as amended),
        // so the strip needs no special state while it's out — just resume the collapse
        // countdown when it goes away.
        rail.OpenChanged += open =>
        {
            if (!open && AutoHideActive && !IsMouseOver && !_vm.AnyAttention)
            {
                RestartCollapseTimer();
            }
        };
    }

    /// <summary>Raised when Explorer restarts; taskbar buttons and DWM colors need re-applying (ADR-0009).</summary>
    public event Action? TaskbarRecreated;

    // App-bar mode wins: re-registering the reserved band on every collapse isn't worth it (ADR-0010).
    private bool AutoHideActive => _vm.Settings.AutoHide && !_vm.Settings.ReserveScreenSpace;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Tool window + no-activate: invisible to Alt-Tab, never steals focus from the terminal.
        var hwnd = new WindowInteropHelper(this).Handle;
        WindowStyler.MakeNonActivatingToolWindow(hwnd);
        HwndSource.FromHwnd(hwnd)?.AddHook(OnWindowMessage);
        Dock();
    }

    private nint OnWindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (WindowPlacer.IsDisplayChangeMessage(msg))
        {
            _redockDebounce.Stop();
            _redockDebounce.Start();
        }
        else if (msg == _taskbarVisibility.TaskbarCreatedMessage)
        {
            TaskbarRecreated?.Invoke();
        }
        else if (_appBar.HandleCallback(msg, wParam, lParam) is { } notification)
        {
            switch (notification)
            {
                case AppBarNotification.PositionChanged:
                    _redockDebounce.Stop();
                    _redockDebounce.Start();
                    break;
                case AppBarNotification.FullscreenEntered when _vm.Settings.HideOverFullscreen:
                    SetFullscreenHidden(true);
                    break;
                case AppBarNotification.FullscreenExited:
                    SetFullscreenHidden(false);
                    break;
            }
        }

        return 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _appBar.Remove(); // an orderly exit must not strand the reserved band (ADR-0011)
        base.OnClosed(e);
    }

    private void CheckForegroundFullscreen()
    {
        var s = _vm.Settings;
        if (!s.HideOverFullscreen || s.ReserveScreenSpace)
        {
            return; // app-bar mode is told directly via ABN_FULLSCREENAPP
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != 0)
        {
            SetFullscreenHidden(FullscreenDetector.IsForegroundFullscreen(hwnd));
        }
    }

    private void SetFullscreenHidden(bool hidden)
    {
        if (_fullscreenHidden == hidden)
        {
            return;
        }

        _fullscreenHidden = hidden;
        Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // Re-assert the physical rect after WPF's automatic rescale; the rect stops
        // changing after one pass, so this cannot loop.
        Dock();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _collapseTimer.Stop();

        // With rail-on-hover, the sliver is only the trigger surface: expanding it and then
        // sliding the rail out reads as two chained bounces, so the rail is the one motion
        // and the strip stays collapsed (amends ADR-0010; Attention still expands it, R24).
        if (!(AutoHideActive && _vm.Settings.OpenRailOnHover))
        {
            SetCollapsed(false);
        }

        if (_vm.Settings.OpenRailOnHover && !_rail.IsOpen)
        {
            _hoverOpenTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, _vm.Settings.HoverOpenDelayMs));
            _hoverOpenTimer.Start();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverOpenTimer.Stop();
        if (AutoHideActive)
        {
            RestartCollapseTimer();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(StripViewModel.AnyAttention))
        {
            return;
        }

        // Attention slides the strip out and holds it there until focus clears it
        // (invariant 6); permitted unprompted motion under invariant 10.
        if (_vm.AnyAttention)
        {
            _collapseTimer.Stop();
            SetCollapsed(false);
        }
        else if (AutoHideActive && !IsMouseOver)
        {
            RestartCollapseTimer();
        }
    }

    private void RestartCollapseTimer()
    {
        _collapseTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, _vm.Settings.CollapseDelayMs));
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed)
        {
            return;
        }

        _collapsed = collapsed;
        Dock(animate: true);
    }

    /// <summary>Positions the strip on the configured edge of the configured monitor (ADR-0007).</summary>
    private void Dock(bool animate = false)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
        {
            return; // The VM can fire before the HWND exists; OnSourceInitialized docks then.
        }

        if (FindPanel() is { } panel)
        {
            panel.Columns = _vm.IsVertical ? 1 : 0;
            panel.Rows = _vm.IsVertical ? 0 : 1;
        }

        var monitors = MonitorLayout.Snapshot();
        if (monitors.Count == 0)
        {
            return; // Session transition; keep the last rect until the next display change.
        }

        var s = _vm.Settings;
        if (!AutoHideActive)
        {
            _collapsed = false; // setting toggled off mid-collapse must not strand a sliver
        }

        if (!s.HideOverFullscreen)
        {
            SetFullscreenHidden(false); // live toggle-off must not strand a hidden strip
        }

        var monitor = StripPlacement.SelectMonitor(monitors, s.Monitor);

        if (s.ReserveScreenSpace)
        {
            // App-bar mode: propose from the full monitor bounds and occupy whatever the
            // shell grants; the work area would include our own band and drift (ADR-0011).
            var granted = _appBar.Apply(
                hwnd, s.Edge, StripPlacement.ComputeAppBar(monitor, s.Edge, s.Thickness));
            _slide.Stop();
            Apply(hwnd, granted ?? StripPlacement.Compute(monitor, s.Edge, s.Thickness));
            return;
        }

        _appBar.Remove();
        var thickness = _collapsed ? Math.Min(s.HiddenThickness, s.Thickness) : s.Thickness;
        var rect = StripPlacement.Compute(monitor, s.Edge, thickness);

        if (animate && _currentRect is { } from && from != rect)
        {
            _slide.Start(from, rect);
        }
        else
        {
            _slide.Stop();
            Apply(hwnd, rect);
        }
    }

    private void Apply(nint hwnd, PixelRect rect)
    {
        WindowPlacer.MoveWithoutActivating(hwnd, rect);
        _currentRect = rect;
    }

    private UniformGrid? FindPanel() =>
        Segments.ItemsPanel is null ? null : FindVisualChild<UniformGrid>(Segments);

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

    private async void Segment_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TerminalItemViewModel item })
        {
            await _vm.ActivateCommand.ExecuteAsync(item);
        }
    }
}
