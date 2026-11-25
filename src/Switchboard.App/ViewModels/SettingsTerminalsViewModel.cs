using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;
using Switchboard.Core.WindowsTerminal;
using Switchboard.Windows;

namespace Switchboard.App.ViewModels;

/// <summary>
/// The Terminals page: the registry as a table, with an editor for the selected row.
/// Rows are rebuilt wholesale on every change — unlike the rail and switcher there are no
/// DWM thumbnail registrations hanging off them, so there is nothing to preserve.
/// </summary>
public sealed partial class SettingsTerminalsViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly WtProfileProvider _profileProvider;
    private WtProfileCatalog _profiles;
    private string? _profilesFolder;

    public SettingsTerminalsViewModel(TerminalSession session, WtProfileProvider profileProvider)
    {
        _session = session;
        _profileProvider = profileProvider;
        _profiles = profileProvider.LoadDetails();
        _profilesFolder = session.Registry.TerminalHost.Folder;
        _session.RegistryChanged += Sync;
        _session.RegistryChanged += OnRegistryChanged;
        _session.StateChanged += OnStateChanged;
        Sync();
    }

    /// <summary>
    /// The window calls this when it is shown: the host WT may have created its settings.json,
    /// or it may have been edited, since the page last looked.
    /// </summary>
    public void RefreshFromDisk() => ReloadProfiles();

    /// <summary>Re-reads the profile list, including into an editor that is already open.</summary>
    private void ReloadProfiles()
    {
        _profiles = _profileProvider.LoadDetails();
        Editor?.UseProfiles(_profiles);
    }

    private void OnRegistryChanged()
    {
        // A host-folder change (General page or a hand-edit) points at a different settings.json.
        if (_session.Registry.TerminalHost.Folder != _profilesFolder)
        {
            _profilesFolder = _session.Registry.TerminalHost.Folder;
            ReloadProfiles();
        }
    }

    public ObservableCollection<SettingsTerminalRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanMoveUp), nameof(CanMoveDown))]
    private SettingsTerminalRowViewModel? _selectedRow;

    [ObservableProperty]
    private TerminalEditorViewModel? _editor;

    [ObservableProperty]
    private string _filter = string.Empty;

    public bool HasSelection => SelectedRow is not null;

    public bool CanMoveUp => SelectedRow is { } row && Rows.IndexOf(row) > 0 && Filter.Length == 0;

    public bool CanMoveDown => SelectedRow is { } row && Rows.IndexOf(row) < Rows.Count - 1 && Filter.Length == 0;

    partial void OnSelectedRowChanged(SettingsTerminalRowViewModel? value)
    {
        Editor = value is null ? null : NewEditor(value.Definition, isNew: false);
    }

    partial void OnFilterChanged(string value) => Sync();

    [RelayCommand]
    private void Add()
    {
        var seed = new TerminalDefinition
        {
            Name = NextName(),
            Color = ColorPalette.NextUnused(_session.Registry.Terminals),
            Profile = _session.Registry.QuickLaunch.DefaultProfile,
            Shell = _session.Registry.QuickLaunch.DefaultShell,
            Distro = _session.Registry.QuickLaunch.DefaultDistro,
        };

        SelectedRow = null;
        Editor = NewEditor(seed, isNew: true);
    }

    [RelayCommand]
    private async Task Remove()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        await _session.RemoveTerminalAsync(row.Name);
    }

    [RelayCommand]
    private Task MoveUp() => MoveBy(-1);

    [RelayCommand]
    private Task MoveDown() => MoveBy(1);

    private Task MoveBy(int delta)
    {
        // Reordering a filtered view would move rows past ones the user can't see.
        if (SelectedRow is not { } row || Filter.Length > 0)
        {
            return Task.CompletedTask;
        }

        var order = Rows.Select(r => r.Name).ToList();
        var from = order.IndexOf(row.Name);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= order.Count)
        {
            return Task.CompletedTask;
        }

        (order[from], order[to]) = (order[to], order[from]);
        return _session.ReorderTerminalsAsync(order);
    }

    private TerminalEditorViewModel NewEditor(TerminalDefinition definition, bool isNew)
    {
        var editor = new TerminalEditorViewModel(_session, definition, isNew, _profiles);
        editor.Saved += name => Select(name);
        return editor;
    }

    private string NextName()
    {
        var prefix = _session.Registry.QuickLaunch.NamePrefix;
        var taken = _session.Registry.Terminals.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        for (var i = 1; ; i++)
        {
            var candidate = $"{prefix}{i}";
            if (taken.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private void Select(string name)
    {
        SelectedRow = Rows.FirstOrDefault(r => r.Name == name);
    }

    private void Sync()
    {
        var selected = SelectedRow?.Name;
        Rows.Clear();

        var position = 0;
        foreach (var def in _session.Registry.Terminals)
        {
            position++;
            if (Filter.Length > 0 && !def.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Rows.Add(new SettingsTerminalRowViewModel(def, position) { State = _session.StateOf(def.Name) });
        }

        // Re-selecting drops any half-typed edit, which is the honest outcome: the row the
        // editor was bound to has been replaced underneath it.
        SelectedRow = selected is null ? null : Rows.FirstOrDefault(r => r.Name == selected);
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    private void OnStateChanged(string name, TerminalState state)
    {
        if (Rows.FirstOrDefault(r => r.Name == name) is { } row)
        {
            row.State = state;
        }
    }
}
