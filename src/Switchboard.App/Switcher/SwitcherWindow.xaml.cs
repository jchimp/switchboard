using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Switchboard.App.Overlays;
using Switchboard.App.Services;
using Switchboard.App.ViewModels;
using Switchboard.Core.Screen;
using Switchboard.Windows;
using Switchboard.Windows.Tracking;

namespace Switchboard.App.Switcher;

/// <summary>
/// The Ctrl+` overlay switcher (ADR-0016): a centered card grid with live DWM thumbnails,
/// fuzzy filter, arrows, 1–9, Enter and Esc. Activatable by design — the sanctioned
/// exception to invariant 7 that ADR-0012 reserved for this window.
/// </summary>
public partial class SwitcherWindow : ActivatableOverlayWindow, IDisposable
{
    private readonly SwitcherViewModel _vm;
    private readonly TerminalSession _session;
    private readonly ILogger<WindowThumbnail> _thumbLog;
    private readonly ILogger<SwitcherWindow> _log;
    private readonly Dictionary<string, WindowThumbnail> _thumbnails = new(StringComparer.Ordinal);

    public SwitcherWindow(
        SwitcherViewModel vm, TerminalSession session,
        ILogger<WindowThumbnail> thumbLog, ILogger<SwitcherWindow> log)
    {
        _vm = vm;
        _session = session;
        _thumbLog = thumbLog;
        _log = log;
        InitializeComponent();
        DataContext = vm;

        _vm.Items.CollectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(SyncThumbnails, System.Windows.Threading.DispatcherPriority.Background);
        // Filtering shows/hides cards through the view; their containers move, so resync.
        ((INotifyCollectionChanged)_vm.View).CollectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(SyncThumbnails, System.Windows.Threading.DispatcherPriority.Background);
        _session.StateChanged += (_, _) => SyncThumbnails();
        SizeChanged += (_, _) => SyncThumbnails();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                UnregisterAllThumbnails(); // the destination window went invisible
            }
        };
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            CloseRestoringFocus(); // the chord while open reads as "never mind", like Esc
        }
        else
        {
            OpenOverlay();
        }
    }

    protected override void OnOpening()
    {
        _vm.PrepareToOpen();
        Opacity = 0; // stay invisible until Center() has run, so there is no jump-flash
    }

    protected override void OnOpened()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                CenterOnMonitor(_session.Registry.Strip.Monitor);
                Opacity = 1;
                FilterBox.Focus();
                SyncThumbnails();
            },
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.Escape:
                    e.Handled = true;
                    CloseRestoringFocus();
                    return;
                case Key.Enter:
                    e.Handled = true;
                    CloseWithoutRestore();
                    await _vm.ActivateSelectedAsync();
                    return;
                case Key.Left:
                    e.Handled = true;
                    _vm.MoveSelection(-1);
                    return;
                case Key.Right:
                    e.Handled = true;
                    _vm.MoveSelection(+1);
                    return;
                case Key.Up:
                    e.Handled = true;
                    _vm.MoveSelection(-_vm.Columns);
                    return;
                case Key.Down:
                    e.Handled = true;
                    _vm.MoveSelection(+_vm.Columns);
                    return;
            }

            // Digits jump (strip-badge semantics) only while the filter is empty; once the
            // user is typing, digits belong to the query — names may contain them.
            if (_vm.Query.Length == 0 && DigitOf(e.Key) is { } digit)
            {
                e.Handled = true;
                CloseWithoutRestore();
                await _vm.ActivateByJumpKeyAsync(digit);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Switcher key handling failed");
        }
    }

    private static int? DigitOf(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null,
    };

    private async void Card_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SwitcherRowViewModel item })
        {
            CloseWithoutRestore();
            await _session.ActivateAsync(item.Name);
        }
    }

    /// <summary>
    /// Registers/repositions a DWM thumbnail for every visible card with a live window —
    /// the same one-destination-window mechanics as the rail (ADR-0012 Slice 3).
    /// </summary>
    private void SyncThumbnails()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0 || !IsVisible)
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

            if (Cards.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter presenter ||
                presenter.ContentTemplate?.FindName("ThumbSlot", presenter) is not FrameworkElement slot ||
                slot.ActualWidth <= 0 || slot.ActualHeight <= 0)
            {
                continue; // filtered out, or not realized yet
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
}
