using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Switchboard.Core.Registry;
using Switchboard.App.ViewModels;
using Switchboard.Windows;

namespace Switchboard.App.Settings;

/// <summary>
/// The manager's own window (ADR-0018). Deliberately the least exotic window in the product:
/// it activates, it sits in the taskbar and Alt-Tab, it resizes, and it leaves the foreground
/// alone when it closes. The strip, rail and overlays are all built to get out of your way;
/// this one is a place you go.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly Func<ImportViewModel> _importViewModel;
    private readonly ILogger<SettingsWindow> _log;

    public SettingsWindow(SettingsViewModel vm, Func<ImportViewModel> importViewModel, ILogger<SettingsWindow> log)
    {
        _vm = vm;
        _importViewModel = importViewModel;
        _log = log;
        DataContext = vm;
        InitializeComponent();

        // Before the first Show(): CenterScreen centers the window it is given, so it must be
        // given the remembered size, not grow from the default's corner afterwards.
        ApplyLayout(vm.Layout);
        SourceInitialized += (_, _) => DarkTitleBar.Apply(new WindowInteropHelper(this).Handle);
        vm.Terminals.ImportRequested += ShowImportDialog;

        // Hidden, not closed, between uses: re-check the WT files each time it is shown.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                vm.General.RefreshFromDisk();
                vm.Terminals.RefreshFromDisk();
            }
        };
    }

    private void ShowImportDialog()
    {
        var dialog = new ImportWindowsDialog(_importViewModel()) { Owner = this };
        dialog.ShowDialog();
    }

    /// <summary>Opens the window, or brings it forward when it is already up.</summary>
    public void Open()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <summary>
    /// Chord capture. The box is read-only and takes the chord from the keys themselves, so
    /// there is nothing to mistype; Esc leaves the existing chord alone.
    /// </summary>
    private void Chord_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: HotkeyRowViewModel row })
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            Keyboard.ClearFocus();
            return;
        }

        // Wait for a real key: a bare modifier is the user still assembling the chord.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        if (row.IsPrefix)
        {
            // The prefix is modifiers only; the digit is appended when the jump keys bind.
            if (parts.Count > 0)
            {
                row.Chord = string.Join("+", parts);
            }

            return;
        }

        if (parts.Count == 0)
        {
            return; // HotkeyChord rejects modifier-less chords, and so should the capture.
        }

        parts.Add(KeyName(key));
        row.Chord = string.Join("+", parts);
    }

    /// <summary>Maps a WPF key to the spelling <see cref="Switchboard.Core.Hotkeys.HotkeyChord"/> parses.</summary>
    private static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        Key.OemTilde => "`",
        Key.Space => "Space",
        Key.Tab => "Tab",
        Key.Enter => "Enter",
        Key.OemMinus => "-",
        Key.OemPlus => "+",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemPipe => "\\",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        _ => key.ToString(),
    };

    private void Chord_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: HotkeyRowViewModel row })
        {
            row.IsCapturing = true;
        }
    }

    private void Chord_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: HotkeyRowViewModel row })
        {
            row.IsCapturing = false;
        }
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        // Read before Hide(): a hidden window still has its size, but keep the order obvious.
        _vm.RememberLayout(CurrentLayout());

        // A DI singleton whose view models stay subscribed to the session: hide it rather than
        // destroying the HWND a later Open() would need. Real shutdown still tears it down.
        if (Application.Current?.Dispatcher.HasShutdownStarted != true)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);

        // Anything still sitting in the debounce belongs in the file before we go quiet.
        // async void (an override): a failed flush must be logged, not crash the app.
        try
        {
            await _vm.FlushAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Saving settings as the window closed failed");
        }
    }

    /// <summary>
    /// The splitter honours both columns' minimums while dragging; resizing the window does not.
    /// Keep the editor narrow enough that the list keeps its MinWidth, so shrinking the window
    /// after widening the editor never pushes the editor's fields off the edge.
    /// </summary>
    private void CapEditorWidth(object sender, SizeChangedEventArgs e)
    {
        if (TerminalsSplit.ActualWidth <= 0 || !EditorColumn.Width.IsAbsolute)
        {
            return; // collapsed (another page is showing), or not laid out yet
        }

        var list = TerminalsSplit.ColumnDefinitions[0].MinWidth;
        var splitter = TerminalsSplit.ColumnDefinitions[1].ActualWidth;
        var max = Math.Max(EditorColumn.MinWidth, TerminalsSplit.ActualWidth - list - splitter);
        if (EditorColumn.Width.Value > max)
        {
            EditorColumn.Width = new GridLength(max);
        }
    }

    /// <summary>Restores the remembered size and the Terminals page's list/editor split.</summary>
    private void ApplyLayout(SettingsWindowLayout layout)
    {
        if (WindowState == WindowState.Normal)
        {
            Width = layout.Width;
            Height = layout.Height;
        }

        EditorColumn.Width = new GridLength(layout.EditorWidth);
    }

    /// <summary>
    /// The size and split as the user left them. A maximized window reports its normal-state
    /// size through RestoreBounds, so un-maximizing next time lands where it was.
    /// </summary>
    private SettingsWindowLayout CurrentLayout()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(0, 0, ActualWidth, ActualHeight) : RestoreBounds;
        return new SettingsWindowLayout
        {
            Width = (int)Math.Round(bounds.Width),
            Height = (int)Math.Round(bounds.Height),
            // The column's set width, not ActualWidth: closing from another page leaves the
            // Terminals grid collapsed, where ActualWidth is 0. The splitter keeps Width current.
            EditorWidth = (int)Math.Round(EditorColumn.Width.IsAbsolute ? EditorColumn.Width.Value : EditorColumn.ActualWidth),
        };
    }
}
