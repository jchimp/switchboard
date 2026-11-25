using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

public sealed partial class StripViewModel : ObservableObject, ISwitchboardMenuCommands
{
    private readonly TerminalSession _session;
    private readonly QuickLaunchService _quick;
    private readonly SettingsLauncher _settings;

    public StripViewModel(TerminalSession session, QuickLaunchService quick, SettingsLauncher settings)
    {
        _session = session;
        _quick = quick;
        _settings = settings;
        _session.StateChanged += OnStateChanged;
        _session.RegistryChanged += Rebuild;
        _session.ForegroundChanged += OnForegroundChanged;
        Rebuild();
    }

    public ObservableCollection<TerminalItemViewModel> Items { get; } = [];

    public StripSettings Settings => _session.Registry.Strip;

    public bool IsVertical => Settings.Edge is ScreenEdge.Left or ScreenEdge.Right;

    /// <summary>True while auto-hide has the strip down to its sliver. Set by the window.</summary>
    [ObservableProperty]
    private bool _isCollapsed;

    [RelayCommand]
    private Task Activate(TerminalItemViewModel item) => _session.ActivateAsync(item.Name, toggleMinimize: true);

    [RelayCommand]
    private Task NewTerminal() => _quick.CreateAndLaunchAsync();

    [RelayCommand]
    private void OpenSettings() => _settings.Open();

    [RelayCommand]
    private void ReapplyColors() => _session.ReapplyColors();

    [RelayCommand]
    private Task ReloadRegistry() => _session.ReloadRegistryAsync();

    [RelayCommand]
    private void Quit() => System.Windows.Application.Current.Shutdown();

    private void Rebuild()
    {
        Items.Clear();
        foreach (var def in _session.Registry.Terminals)
        {
            if (!Settings.ShowClosedTerminals && _session.StateOf(def.Name) == TerminalState.Closed)
            {
                continue;
            }

            Items.Add(new TerminalItemViewModel(def)
            {
                State = _session.StateOf(def.Name),
                IsActive = def.Name == _session.ForegroundName,
            });
        }
    }

    private void OnForegroundChanged(string? name)
    {
        foreach (var item in Items)
        {
            item.IsActive = item.Name == name;
        }
    }

    private void OnStateChanged(string name, TerminalState state)
    {
        // With ShowClosedTerminals off, a state change can add or remove a segment, not
        // just restyle one; rebuilding keeps registry order trivially correct.
        var item = Items.FirstOrDefault(i => i.Name == name);
        if (item is not null)
        {
            if (!Settings.ShowClosedTerminals && state == TerminalState.Closed)
            {
                Rebuild();
                return;
            }

            item.State = state;
            return;
        }

        if ((Settings.ShowClosedTerminals || state != TerminalState.Closed) &&
            _session.Definition(name) is not null)
        {
            Rebuild();
        }
    }
}
