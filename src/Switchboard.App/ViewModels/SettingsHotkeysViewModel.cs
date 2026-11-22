using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.App.Services;
using Switchboard.Core.Registry;

namespace Switchboard.App.ViewModels;

/// <summary>
/// The Hotkeys page. Every chord in <see cref="HotkeySettings"/>, each with a live badge
/// saying whether Windows actually handed it over.
/// </summary>
/// <remarks>
/// The badge is the point of this page as much as the editing is: a chord another app owns
/// was previously only a line in the log, so a hotkey that silently did nothing looked like
/// a Switchboard bug. Rebinding is live — <c>RegistryChanged</c> already drives
/// <see cref="HotkeyBindings.Rebind"/>, so a new chord works the moment it lands.
/// </remarks>
public sealed partial class SettingsHotkeysViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly SettingsApplyQueue _queue;
    private readonly HotkeyBindings _bindings;
    private bool _loading;

    public SettingsHotkeysViewModel(TerminalSession session, SettingsApplyQueue queue, HotkeyBindings bindings)
    {
        _session = session;
        _queue = queue;
        _bindings = bindings;
        _session.RegistryChanged += OnRegistryChanged;
        _bindings.StatusChanged += RefreshBadges;
        Load();
    }

    public ObservableCollection<HotkeyRowViewModel> Rows { get; } = [];

    private void OnChordChanged(HotkeyRowViewModel row)
    {
        if (_loading)
        {
            return;
        }

        _queue.Queue(registry => registry with { Hotkeys = Build(registry.Hotkeys) });
    }

    private HotkeySettings Build(HotkeySettings current)
    {
        string Chord(string action, string fallback) =>
            Rows.FirstOrDefault(r => r.Action == action) is { ParseError: null } row ? row.Chord : fallback;

        return current with
        {
            OpenSwitcher = Chord(Actions.OpenSwitcher, current.OpenSwitcher),
            NewTerminal = Chord(Actions.NewTerminal, current.NewTerminal),
            NewTerminalQuiet = Chord(Actions.NewTerminalQuiet, current.NewTerminalQuiet),
            CycleRecent = Chord(Actions.CycleRecent, current.CycleRecent),
            JumpToAttention = Chord(Actions.JumpToAttention, current.JumpToAttention),
            ToggleRail = Chord(Actions.ToggleRail, current.ToggleRail),
            ReapplyColors = Chord(Actions.ReapplyColors, current.ReapplyColors),
            JumpPrefix = Chord(Actions.JumpPrefix, current.JumpPrefix),
        };
    }

    private void OnRegistryChanged()
    {
        if (!_queue.IsApplyingOwnEdit)
        {
            Load();
        }
    }

    private void RefreshBadges()
    {
        foreach (var row in Rows)
        {
            row.State = _bindings.Status.TryGetValue(RowChordKey(row), out var state) ? state : null;
        }
    }

    /// <summary>Jump keys register as prefix+digit, so the prefix row's status hangs off the first one.</summary>
    private static string RowChordKey(HotkeyRowViewModel row) => row.IsPrefix ? $"{row.Chord}+1" : row.Chord;

    private void Load()
    {
        _loading = true;
        try
        {
            Rows.Clear();
            var h = _session.Registry.Hotkeys;
            Rows.Add(new HotkeyRowViewModel(Actions.OpenSwitcher, "Open the overlay switcher", h.OpenSwitcher, false, OnChordChanged));
            Rows.Add(new HotkeyRowViewModel(Actions.CycleRecent, "Cycle through recent terminals", h.CycleRecent, false, OnChordChanged));
            Rows.Add(new HotkeyRowViewModel(Actions.JumpToAttention, "Jump to the terminal wanting attention", h.JumpToAttention, false, OnChordChanged));
            Rows.Add(new HotkeyRowViewModel(Actions.JumpPrefix, "Prefix for the 1–9 jump keys", h.JumpPrefix, true, OnChordChanged));
            Rows.Add(new HotkeyRowViewModel(Actions.NewTerminal, "New terminal (opens the picker)", h.NewTerminal, false, OnChordChanged));
            Rows.Add(new HotkeyRowViewModel(Actions.NewTerminalQuiet, "New terminal from defaults, no UI", h.NewTerminalQuiet, false, OnChordChanged));
            Rows.Add(new HotkeyRowViewModel(Actions.ToggleRail, "Open or close the rail", h.ToggleRail, false, OnChordChanged));
            Rows.Add(new HotkeyRowViewModel(Actions.ReapplyColors, "Re-apply frame colors", h.ReapplyColors, false, OnChordChanged));
        }
        finally
        {
            _loading = false;
        }

        RefreshBadges();
    }

    /// <summary>Row keys. Strings rather than an enum so the rows stay a flat, ordered list.</summary>
    private static class Actions
    {
        public const string OpenSwitcher = "openSwitcher";
        public const string NewTerminal = "newTerminal";
        public const string NewTerminalQuiet = "newTerminalQuiet";
        public const string CycleRecent = "cycleRecent";
        public const string JumpToAttention = "jumpToAttention";
        public const string ToggleRail = "toggleRail";
        public const string ReapplyColors = "reapplyColors";
        public const string JumpPrefix = "jumpPrefix";
    }
}
