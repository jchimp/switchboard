using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Switchboard.App.Services;
using Switchboard.App.ViewModels;
using Switchboard.Core.Screen;
using Switchboard.Windows;
using Switchboard.Windows.Tracking;

namespace Switchboard.App.Rail;

/// <summary>
/// The slide-out rail: a second non-activating, topmost tool window adjacent to the strip
/// (ADR-0012). Hidden, not closed, between uses. Opens on strip hover or the toggleRail
/// chord; closes when the mouse leaves it or on the chord again. Never takes focus.
/// </summary>
public partial class RailWindow : Window, IDisposable
{
    private const int SlideDurationMs = 220;

    private readonly RailViewModel _vm;
    private readonly TerminalSession _session;
    private readonly ILogger<WindowThumbnail> _thumbLog;
    private readonly Dictionary<string, WindowThumbnail> _thumbnails = new(StringComparer.Ordinal);
    private readonly System.Windows.Threading.DispatcherTimer _slideTimer;
    private readonly System.Windows.Threading.DispatcherTimer _hoverProbe;
    private PixelRect _slideFrom;
    private PixelRect _slideTo;
    private long _slideStartTicks;
    private bool _hidingAfterSlide;
    private bool _tracksHover;
    private bool _menuOpen;

    public RailWindow(RailViewModel vm, TerminalSession session, ILogger<WindowThumbnail> thumbLog)
    {
        _vm = vm;
        _session = session;
        _thumbLog = thumbLog;
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

            // The context menu is outside the rail's bounds; don't close under it.
            if (_tracksHover && !_menuOpen && !IsMouseOver && AnchorHovered?.Invoke() != true)
            {
                CloseRail();
            }
        };

        // Thumbnails must track row position (new/removed rows, scroll, resize) and row
        // liveness (a terminal opening/closing). Cheap when the rail is hidden — all these
        // no-op unless IsOpen.
        _vm.Items.CollectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(SyncThumbnails, System.Windows.Threading.DispatcherPriority.Background);
        _session.StateChanged += (_, _) => SyncThumbnails();
        SizeChanged += (_, _) => SyncThumbnails();
    }

    public bool IsOpen { get; private set; }

    /// <summary>True while the open/close slide is animating; motion sampling yields to it.</summary>
    public bool IsSliding => _slideTimer.IsEnabled;

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

        // Zero strip offset: the rail anchors at the screen edge and covers the strip while
        // open (Concept A, ADR-0012 as amended) — its geometry never depends on whether the
        // strip is currently a sliver, full, or attention-expanded.
        var open = RailPlacement.Compute(monitor, s.Edge, 0, size);
        var hidden = RailPlacement.ComputeHidden(monitor, s.Edge, 0, size);

        ApplyOrientation(vertical);

        _vm.Sync(); // events keep it fresh, but a cheap resync on open is bulletproof
        _vm.RefreshUnbound(); // R7: the one EnumWindows-shaped scan, run only on open
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

        // After the first layout pass: re-apply orientation (the ItemsPanel doesn't exist
        // until the window has rendered once, so the pre-Show attempt found no panel on the
        // very first open) and register thumbnails — their rects are client-relative, so
        // they ride along during the slide instead of popping in after.
        Dispatcher.BeginInvoke(
            () =>
            {
                ApplyOrientation(vertical);
                SyncThumbnails();
            },
            System.Windows.Threading.DispatcherPriority.Loaded);
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

    private void ApplyOrientation(bool vertical)
    {
        if (FindPanel() is { } panel)
        {
            panel.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        }

        ListScroller.VerticalScrollBarVisibility = vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        ListScroller.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

        // The 150-dip top-edge rail has no height for a bottom-docked foot: the unbound
        // section alone would squeeze the card list to nothing. Concept A rotates the foot
        // into a right-hand column there.
        DockPanel.SetDock(FootSection, vertical ? Dock.Bottom : Dock.Right);
        FootSection.Width = vertical ? double.NaN : 250;
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
        return RailPlacement.ComputeHidden(StripPlacement.SelectMonitor(monitors, s.Monitor), s.Edge, 0, size);
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
                UnregisterAllThumbnails(); // the destination window is going invisible
                Hide();
            }
            else
            {
                SyncThumbnails(); // open slide finished; rows are laid out, positions are final
            }
        }
    }

    private void ListScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (IsOpen)
        {
            SyncThumbnails();
        }
    }

    /// <summary>
    /// Registers/repositions a DWM thumbnail for every row with a live window, and drops
    /// thumbnails for rows that closed or scrolled/resized away. All rects are relative to
    /// this window's own client area — one destination window hosts every row (Slice 3).
    /// </summary>
    private void SyncThumbnails()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0 || !IsOpen)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in _vm.Items)
        {
            var source = _session.HwndOf(item.Name);
            if (source == 0)
            {
                continue; // Closed; the placeholder background shows through
            }

            if (Rows.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter presenter ||
                presenter.ContentTemplate?.FindName("ThumbSlot", presenter) is not FrameworkElement slot ||
                slot.ActualWidth <= 0 || slot.ActualHeight <= 0)
            {
                continue; // not realized yet, or scrolled out of the visual tree
            }

            Point origin;
            try
            {
                origin = slot.TranslatePoint(new Point(0, 0), this);
            }
            catch (InvalidOperationException)
            {
                continue; // disconnected from the visual tree mid-layout-pass
            }

            var rect = new PixelRect(
                (int)Math.Round(origin.X * dpi.DpiScaleX),
                (int)Math.Round(origin.Y * dpi.DpiScaleY),
                (int)Math.Round(slot.ActualWidth * dpi.DpiScaleX),
                (int)Math.Round(slot.ActualHeight * dpi.DpiScaleY));

            if (!_thumbnails.TryGetValue(item.Name, out var thumb))
            {
                thumb = new WindowThumbnail(_thumbLog);
                if (!thumb.Register(hwnd, source))
                {
                    continue;
                }

                _thumbnails[item.Name] = thumb;
            }
            else if (!thumb.IsRegistered && !thumb.Register(hwnd, source))
            {
                continue; // the terminal relaunched under the same name; re-registration failed
            }

            thumb.UpdateDestination(rect);
            seen.Add(item.Name);
        }

        foreach (var name in _thumbnails.Keys.Where(n => !seen.Contains(n)).ToList())
        {
            _thumbnails[name].Dispose();
            _thumbnails.Remove(name);
        }
    }

    private void UnregisterAllThumbnails()
    {
        foreach (var thumb in _thumbnails.Values)
        {
            thumb.Dispose();
        }

        _thumbnails.Clear();
    }

    public void Dispose() => UnregisterAllThumbnails();

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

    private void RailMenu_Opened(object sender, RoutedEventArgs e) => _menuOpen = true;

    private void RailMenu_Closed(object sender, RoutedEventArgs e) => _menuOpen = false;

    private void Refresh_Click(object sender, MouseButtonEventArgs e) => _vm.RefreshUnbound();

    private async void NewTerminal_Click(object sender, MouseButtonEventArgs e)
    {
        CloseRail();
        await _vm.NewTerminalCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// "Adopt as…": a small menu of Closed registered terminals plus "New terminal…", built
    /// per click since the offer list changes with registry state. The menu is the same
    /// sanctioned focus escape hatch as the strip's context menu (ADR-0006/0012).
    /// </summary>
    private void AdoptButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UnboundWindow window } element)
        {
            return;
        }

        var menu = new ContextMenu();
        menu.Opened += RailMenu_Opened;
        menu.Closed += RailMenu_Closed;

        foreach (var def in _vm.AdoptableTerminals())
        {
            var item = new MenuItem { Header = def.Name };
            item.Click += (_, _) => _vm.Adopt(window.Hwnd, def.Name);
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        var newItem = new MenuItem { Header = "New terminal…" };
        newItem.Click += (_, _) =>
        {
            var seed = _vm.AdoptSeed();
            ShowEditPopup(element, "Adopt as new terminal", seed.Name, seed.Color,
                (name, color) => _vm.AdoptAsNewAsync(window.Hwnd, name, color));
        };
        menu.Items.Add(newItem);

        element.ContextMenu = menu;
        menu.PlacementTarget = element;
        menu.IsOpen = true;
    }

    private void Rename_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // must not bubble into Row_Click and focus the terminal
        if (sender is FrameworkElement { DataContext: RailItemViewModel item } element)
        {
            ShowEditPopup(element, $"Edit {item.Name}", item.Name, item.Definition.Color,
                (name, color) => _vm.UpdateTerminalAsync(item.Name, name, color));
        }
    }

    /// <summary>
    /// Opens the one activatable window in the product (ADR-0012/0014), anchored under the
    /// clicked element. Modal; the hover probe holds while it's up, and the popup restores
    /// the previous foreground window itself when it closes.
    /// </summary>
    private void ShowEditPopup(
        FrameworkElement anchor, string title, string name, string color,
        Func<string, string, Task<string?>> commit)
    {
        var popup = new RailEditPopup(title, name, color, commit);
        var dpi = VisualTreeHelper.GetDpi(this);
        var below = anchor.PointToScreen(new Point(0, anchor.ActualHeight + 4));
        popup.Left = below.X / dpi.DpiScaleX;
        popup.Top = below.Y / dpi.DpiScaleY;

        _menuOpen = true;
        try
        {
            popup.ShowDialog();
        }
        finally
        {
            _menuOpen = false;
        }
    }
}
