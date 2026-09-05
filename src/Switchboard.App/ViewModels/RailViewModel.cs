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
public sealed partial class RailViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly QuickLaunchService _quick;

    public RailViewModel(TerminalSession session, QuickLaunchService quick)
    {
        _session = session;
        _quick = quick;
        _session.StateChanged += OnStateChanged;
        _session.RegistryChanged += Sync;
        _session.ForegroundChanged += OnForegroundChanged;
        Sync();
    }

    public ObservableCollection<RailItemViewModel> Items { get; } = [];

    /// <summary>
    /// Windows Terminal windows on the desktop not bound to any registered name. Refreshed
    /// only when the rail opens (or the user asks) — never on a timer (R7).
    /// </summary>
    public ObservableCollection<UnboundWindow> UnboundItems { get; } = [];

    public bool HasUnbound => UnboundItems.Count > 0;

    public StripSettings Settings => _session.Registry.Strip;

    public bool IsVertical => RailPlacementIsVertical;

    public string CountLabel => $"{Items.Count(i => !i.IsClosed)} windows";

    private bool RailPlacementIsVertical => Settings.Edge is ScreenEdge.Left or ScreenEdge.Right;

    [RelayCommand]
    private Task Activate(RailItemViewModel item) => _session.ActivateAsync(item.Name);

    [RelayCommand]
    private Task NewTerminal() => _quick.CreateAndLaunchAsync();

    // The rail covers the strip while open (ADR-0012 as amended), so it carries the same
    // context-menu actions the strip has — otherwise they'd be unreachable under it.
    [RelayCommand]
    private void ReapplyColors() => _session.ReapplyColors();

    [RelayCommand]
    private Task ReloadRegistry() => _session.ReloadRegistryAsync();

    [RelayCommand]
    private void Quit() => System.Windows.Application.Current.Shutdown();

    /// <summary>Re-enumerates unbound windows. Call on rail-open, and from the manual Refresh affordance.</summary>
    public void RefreshUnbound()
    {
        UnboundItems.Clear();
        foreach (var w in _session.UnboundWindows())
        {
            UnboundItems.Add(w);
        }

        OnPropertyChanged(nameof(HasUnbound));
    }

    /// <summary>Registered terminals with no live window — the offer list for "adopt as…" (R7).</summary>
    public IReadOnlyList<TerminalDefinition> AdoptableTerminals() => _session.AdoptableTerminals();

    public bool Adopt(nint hwnd, string name)
    {
        var ok = _session.AdoptAsync(name, hwnd);
        if (ok)
        {
            RefreshUnbound();
        }

        return ok;
    }

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
            return "That window is gone; refresh the list.";
        }

        RefreshUnbound();
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

        OnPropertyChanged(nameof(CountLabel));
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
            OnPropertyChanged(nameof(CountLabel));
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
