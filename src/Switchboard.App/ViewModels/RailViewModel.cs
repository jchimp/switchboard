using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;
using Switchboard.Windows.Tracking;

namespace Switchboard.App.ViewModels;

/// <summary>
/// Backs the rail. Unlike the strip's rebuild-from-scratch, rows are synced incrementally
/// and keyed by name: Slice 3 hangs a DWM thumbnail registration off each row, and a
/// Clear() on every state change would churn them all (ADR-0012).
/// </summary>
public sealed partial class RailViewModel : ObservableObject, ISwitchboardMenuCommands
{
    private readonly TerminalSession _session;
    private readonly QuickLaunchService _quick;
    private readonly SettingsLauncher _settings;

    public RailViewModel(TerminalSession session, QuickLaunchService quick, SettingsLauncher settings)
    {
        _session = session;
        _quick = quick;
        _settings = settings;
        _session.StateChanged += OnStateChanged;
        _session.RegistryChanged += Sync;
        _session.ForegroundChanged += OnForegroundChanged;
        Sync();
    }

    public ObservableCollection<RailItemViewModel> Items { get; } = [];

    public StripSettings Settings => _session.Registry.Strip;

    public bool IsVertical => RailPlacementIsVertical;

    private bool RailPlacementIsVertical => Settings.Edge is ScreenEdge.Left or ScreenEdge.Right;

    [RelayCommand]
    private Task Activate(RailItemViewModel item) => _session.ActivateAsync(item.Name, toggleMinimize: true);

    [RelayCommand]
    private Task NewTerminal() => _quick.CreateAndLaunchAsync();

    // The rail covers the strip while open (ADR-0012 as amended), so it carries the same
    // context-menu actions the strip has — otherwise they'd be unreachable under it.
    [RelayCommand]
    private void OpenSettings() => _settings.Open();

    [RelayCommand]
    private void ReapplyColors() => _session.ReapplyColors();

    [RelayCommand]
    private Task ReloadRegistry() => _session.ReloadRegistryAsync();

    [RelayCommand]
    private void Quit() => System.Windows.Application.Current.Shutdown();

    /// <summary>
    /// The terminal host's windows not bound to any registered name. An EnumWindows scan:
    /// call only when the rail's "…" menu opens, never on a timer (R7).
    /// </summary>
    public IReadOnlyList<UnboundWindow> UnboundWindows() => _session.UnboundWindows();

    /// <summary>Registered terminals with no live window — the offer list for "adopt as…" (R7).</summary>
    public IReadOnlyList<TerminalDefinition> AdoptableTerminals() => _session.AdoptableTerminals();

    public bool Adopt(nint hwnd, string name) => _session.AdoptAsync(name, hwnd);

    /// <summary>Name/color defaults for the adopt-as-new popup (next free name, next unused hue).</summary>
    public TerminalDefinition AdoptSeed() => _quick.BuildDefinition(persistent: true);

    /// <summary>Popup commit for adopt-as-new. Returns a user-facing error, or null on success.</summary>
    public async Task<string?> AdoptAsNewAsync(nint hwnd, string name, string color)
    {
        var def = AdoptSeed() with { Name = name, Color = color };
        if (def.Validate() is { } error)
        {
            return error;
        }

        if (_session.Definition(def.Name) is not null)
        {
            return $"Name '{def.Name}' is already in use.";
        }

        if (!await _session.AdoptAsNewAsync(def, hwnd))
        {
            return "That window is gone.";
        }

        return null;
    }

    /// <summary>Popup commit for rename/recolor (ADR-0014). Returns a user-facing error, or null.</summary>
    public async Task<string?> UpdateTerminalAsync(string oldName, string newName, string color)
    {
        if (_session.Definition(oldName) is not { } def)
        {
            return "That terminal no longer exists.";
        }

        try
        {
            await _session.UpdateTerminalAsync(oldName, def with { Name = newName, Color = color });
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Incremental resync against the registry: upsert in order, drop the missing.</summary>
    public void Sync()
    {
        var defs = _session.Registry.Terminals;

        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!defs.Any(d => d.Name == Items[i].Name))
            {
                Items.RemoveAt(i);
            }
        }

        for (var index = 0; index < defs.Count; index++)
        {
            var def = defs[index];
            var at = IndexOf(def.Name);
            var replace = at >= 0 && !Items[at].Definition.Equals(def);
            if (at >= 0 && !replace && Items[at].Position == index + 1)
            {
                if (at != index)
                {
                    Items.Move(at, index);
                }

                continue;
            }

            if (at >= 0)
            {
                Items.RemoveAt(at);
            }

            Items.Insert(Math.Min(index, Items.Count), new RailItemViewModel(def, index + 1)
            {
                State = _session.StateOf(def.Name),
                IsActive = def.Name == _session.ForegroundName,
            });
        }

        OnPropertyChanged(nameof(IsVertical));
    }

    private int IndexOf(string name)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Name == name)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnStateChanged(string name, TerminalState state)
    {
        if (Items.FirstOrDefault(i => i.Name == name) is { } item)
        {
            item.State = state;
        }
        else if (_session.Definition(name) is not null)
        {
            Sync();
        }
    }

    private void OnForegroundChanged(string? name)
    {
        foreach (var item in Items)
        {
            item.IsActive = item.Name == name;
        }
    }
}
