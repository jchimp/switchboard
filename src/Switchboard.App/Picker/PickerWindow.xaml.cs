using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Switchboard.App.Overlays;
using Switchboard.App.Services;
using Switchboard.App.ViewModels;

namespace Switchboard.App.Picker;

/// <summary>
/// The Ctrl+Alt+N quick-launch picker (ADR-0017): profile via Tab, directory box, "keep"
/// toggle, Enter launches. Opens fully seeded from the quick-launch defaults, so a bare
/// Enter reproduces the old straight-to-launch behaviour. Same activation contract as the
/// switcher (ADR-0016).
/// </summary>
public partial class PickerWindow : ActivatableOverlayWindow
{
    private readonly PickerViewModel _vm;
    private readonly TerminalSession _session;
    private readonly ILogger<PickerWindow> _log;

    public PickerWindow(PickerViewModel vm, TerminalSession session, ILogger<PickerWindow> log)
    {
        _vm = vm;
        _session = session;
        _log = log;
        InitializeComponent();
        DataContext = vm;
    }

    public void Open() => OpenOverlay();

    protected override void OnOpening()
    {
        _vm.PrepareToOpen();
        Opacity = 0; // invisible until centered, so there is no jump-flash
    }

    protected override void OnOpened()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                CenterOnMonitor(_session.Registry.Strip.Monitor);
                Opacity = 1;
                DirectoryBox.Focus();
                DirectoryBox.CaretIndex = DirectoryBox.Text.Length;
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
                    await _vm.LaunchAsync();
                    return;
                // Tab is the profile cycle (R22); e.Handled stops WPF's own focus traversal
                // from fighting it. Directory paths may contain spaces, so Space stays a
                // regular keystroke and "keep" toggles on Ctrl+K (or a click) instead.
                case Key.Tab:
                    e.Handled = true;
                    _vm.CycleProfile(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : +1);
                    return;
                case Key.K when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                    e.Handled = true;
                    _vm.Keep = !_vm.Keep;
                    return;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Picker key handling failed");
        }
    }

    private void Chip_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProfileChipViewModel chip })
        {
            _vm.Select(chip);
        }
    }
}
