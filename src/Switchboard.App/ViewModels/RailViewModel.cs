using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;

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
