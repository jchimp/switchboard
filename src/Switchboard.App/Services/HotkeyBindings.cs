using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<HotkeyBindings> _log;
    private HotkeySettings? _bound;

    public HotkeyBindings(GlobalHotkeyManager hotkeys, TerminalSession session, QuickLaunchService quick, ILogger<HotkeyBindings> log)
    {
        _hotkeys = hotkeys;
        _session = session;
        _quick = quick;
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
        Bind(h.NewTerminal, () => Fire(_quick.CreateAndLaunchAsync()));
        Bind(h.CycleRecent, () => Fire(_session.CycleRecentAsync()));
        Bind(h.JumpToAttention, () => Fire(_session.ActivateAttentionAsync()));
        Bind(h.ReapplyColors, _session.ReapplyColors);
        // OpenSwitcher / ToggleRail land in milestone 2 with their windows.

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
