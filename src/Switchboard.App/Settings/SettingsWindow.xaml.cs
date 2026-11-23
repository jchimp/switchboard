using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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

    public SettingsWindow(SettingsViewModel vm, Func<ImportViewModel> importViewModel)
    {
        _vm = vm;
        _importViewModel = importViewModel;
        DataContext = vm;
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(new WindowInteropHelper(this).Handle);
        vm.Terminals.ImportRequested += ShowImportDialog;

        // Hidden, not closed, between uses: re-check the WT files each time it is shown.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                vm.Terminals.RefreshAppearance();
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
        // A DI singleton whose view models stay subscribed to the session: hide it rather than
        // destroying the HWND a later Open() would need. Real shutdown still tears it down.
        if (Application.Current?.Dispatcher.HasShutdownStarted != true)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);

        // Anything still sitting in the debounce belongs in the file before we go quiet.
        await _vm.FlushAsync();
    }
}
