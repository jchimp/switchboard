using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Switchboard.App.Picker;
using Switchboard.App.Rail;
using Switchboard.App.Switcher;
using Switchboard.Core.Hotkeys;
using Switchboard.Core.Registry;
using Switchboard.Windows.Hotkeys;

namespace Switchboard.App.Services;

/// <summary>Translates <see cref="HotkeySettings"/> into live registrations.</summary>
public sealed class HotkeyBindings
{
    private readonly GlobalHotkeyManager _hotkeys;
    private readonly TerminalSession _session;
    private readonly QuickLaunchService _quick;
    private readonly RailWindow _rail;
    private readonly SwitcherWindow _switcher;
    private readonly PickerWindow _picker;
    private readonly ILogger<HotkeyBindings> _log;
    private HotkeySettings? _bound;

    public HotkeyBindings(
        GlobalHotkeyManager hotkeys, TerminalSession session, QuickLaunchService quick,
        RailWindow rail, SwitcherWindow switcher, PickerWindow picker, ILogger<HotkeyBindings> log)
    {
        _hotkeys = hotkeys;
        _session = session;
        _quick = quick;
        _rail = rail;
        _switcher = switcher;
        _picker = picker;
        _log = log;
    }

    /// <summary>One-time hookup to the host window, then the initial bind.</summary>
    public void Attach(Window host)
    {
        var source = (HwndSource)PresentationSource.FromVisual(host)!;
        source.AddHook(WndProc);
        _hotkeys.Attach(source.Handle);
        Rebind();
    }

    /// <summary>
    /// Re-registers everything from the current registry. RegistryChanged fires for terminal
    /// edits too, so this is a no-op unless the hotkey settings themselves changed.
    /// </summary>
    public void Rebind()
    {
        var h = _session.Registry.Hotkeys;
        if (h == _bound)
        {
            return;
        }

        _hotkeys.UnregisterAll();
        _bound = h;
        Bind(h.NewTerminal, _picker.Open);
        Bind(h.NewTerminalQuiet, () => Fire(_quick.CreateAndLaunchAsync()));
        Bind(h.CycleRecent, () => Fire(_session.CycleRecentAsync()));
        Bind(h.JumpToAttention, () => Fire(_session.ActivateAttentionAsync()));
        Bind(h.ReapplyColors, _session.ReapplyColors);
        Bind(h.ToggleRail, _rail.Toggle);
        Bind(h.OpenSwitcher, _switcher.Toggle);

        for (var d = 1; d <= 9; d++)
        {
            var digit = d;
            Bind($"{h.JumpPrefix}+{digit}", () => Fire(_session.ActivateByJumpKeyAsync(digit)));
        }
    }

    private void Bind(string chordText, Action action)
    {
        if (!HotkeyChord.TryParse(chordText, out var chord, out var error))
        {
            _log.LogWarning("Skipping hotkey '{Text}': {Error}", chordText, error);
            return;
        }

        _hotkeys.Register(chord, action);
    }

    private async void Fire(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Hotkey action failed");
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        handled = _hotkeys.HandleMessage(msg, wParam);
        return 0;
    }
}
