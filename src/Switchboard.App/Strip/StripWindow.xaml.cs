using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
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
    private readonly System.Windows.Threading.DispatcherTimer _redockDebounce;
    private readonly System.Windows.Threading.DispatcherTimer _collapseTimer;
    private readonly System.Windows.Threading.DispatcherTimer _slideTimer;
    private bool _collapsed;
    private PixelRect? _currentRect;
    private PixelRect _slideFrom;
    private PixelRect _slideTo;
    private long _slideStartTicks;

    public StripWindow(StripViewModel vm, TaskbarVisibility taskbarVisibility)
    {
        _vm = vm;
        _taskbarVisibility = taskbarVisibility;
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
                SetCollapsed(true);
            }
        };

        _slideTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _slideTimer.Tick += (_, _) => SlideStep();
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

        return 0;
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
        SetCollapsed(false);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
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

        var thickness = _collapsed ? Math.Min(s.HiddenThickness, s.Thickness) : s.Thickness;
        var monitor = StripPlacement.SelectMonitor(monitors, s.Monitor);
        var rect = StripPlacement.Compute(monitor, s.Edge, thickness);

        if (animate && _currentRect is { } from && from != rect)
        {
            _slideFrom = from;
            _slideTo = rect;
            _slideStartTicks = Environment.TickCount64;
            _slideTimer.Start();
        }
        else
        {
            _slideTimer.Stop();
            Apply(hwnd, rect);
        }
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
        var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
        Apply(hwnd, new PixelRect(
            Lerp(_slideFrom.X, _slideTo.X, eased),
            Lerp(_slideFrom.Y, _slideTo.Y, eased),
            Lerp(_slideFrom.Width, _slideTo.Width, eased),
            Lerp(_slideFrom.Height, _slideTo.Height, eased)));

        if (t >= 1.0)
        {
            _slideTimer.Stop();
        }
    }

    private static int Lerp(int from, int to, double t) => from + (int)Math.Round((to - from) * t);

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
