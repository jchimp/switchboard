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
    private readonly SettingsApplyQueue _queue;
    private readonly WtAppearanceImporter _appearance;
    private readonly WtProfileProvider _profileProvider;
    private WtProfileCatalog _profiles;
    private bool _loadingHostFolder;
    private string? _appearanceFolder;

    public SettingsTerminalsViewModel(
        TerminalSession session, SettingsApplyQueue queue, WtAppearanceImporter appearance,
        WtProfileProvider profileProvider)
    {
        _session = session;
        _queue = queue;
        _appearance = appearance;
        _profileProvider = profileProvider;
        _profiles = profileProvider.LoadDetails();
        _session.RegistryChanged += Sync;
        _session.RegistryChanged += OnHostChanged;
        _session.StateChanged += OnStateChanged;
        Sync();
        LoadHostFolder();
        RefreshAppearance();
    }

    /// <summary>What the appearance import can do right now (ADR-0022); drives the button and hint.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImportAppearance), nameof(HostLooksUnstyled))]
    [NotifyCanExecuteChangedFor(nameof(ImportAppearanceCommand))]
    private AppearanceImportState _appearanceState;

    /// <summary>The last import's outcome, shown under the button until the next one.</summary>
    [ObservableProperty]
    private string? _appearanceResult;

    public bool CanImportAppearance => AppearanceState != AppearanceImportState.Unavailable;

    public bool HostLooksUnstyled => AppearanceState == AppearanceImportState.Unstyled;

    /// <summary>One-shot, user-triggered: it overwrites the host's appearance (ADR-0022).</summary>
    [RelayCommand(CanExecute = nameof(CanImportAppearance))]
    private async Task ImportAppearance()
    {
        AppearanceResult = await _appearance.ImportAsync();
        RefreshAppearance();
    }

    /// <summary>Re-reads both WT files for the appearance import. Never on a timer.</summary>
    private void RefreshAppearance() => AppearanceState = _appearance.CurrentState();

    /// <summary>
    /// The window calls this when it is shown: the host WT may have created its settings.json,
    /// or either file been edited, since the page last looked.
    /// </summary>
    public void RefreshFromDisk()
    {
        ReloadProfiles();
        RefreshAppearance();
    }

    /// <summary>Re-reads the profile list, including into an editor that is already open.</summary>
    private void ReloadProfiles()
    {
        _profiles = _profileProvider.LoadDetails();
        Editor?.UseProfiles(_profiles);
    }

    /// <summary>
    /// The private WT folder (ADR-0021), live-applied through the settings queue like the
    /// other pages (ADR-0018). Empty means the installed Windows Terminal.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HostFolderMissing))]
    private string _terminalHostFolder = string.Empty;

    /// <summary>
    /// The typed folder has no wt.exe in it: every launch would fail. Checked on the text, not
    /// on the saved setting, so it updates per keystroke instead of after the debounced save.
    /// </summary>
    public bool HostFolderMissing => TerminalHost.IsMissingAt(TerminalHostFolder);

    partial void OnTerminalHostFolderChanged(string value)
    {
        if (_loadingHostFolder)
        {
            return;
        }

        var folder = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        _queue.Queue(registry => registry with { TerminalHost = registry.TerminalHost with { Folder = folder } });
    }

    private void OnHostChanged()
    {
        // Our own save echoes back; reloading it would rewrite the box mid-typing.
        if (!_queue.IsApplyingOwnEdit)
        {
            LoadHostFolder();
        }

        // The saved folder may now point at a different WT, or at one with no settings yet; an
        // old "Imported…" line would describe the previous host.
        if (_session.Registry.TerminalHost.Folder != _appearanceFolder)
        {
            _appearanceFolder = _session.Registry.TerminalHost.Folder;
            AppearanceResult = null;
        }

        // Same host change: the profile list may now come from a different settings.json.
        ReloadProfiles();
        RefreshAppearance();
    }

    private void LoadHostFolder()
    {
        _loadingHostFolder = true;
        try
        {
            TerminalHostFolder = _session.Registry.TerminalHost.Folder ?? string.Empty;
        }
        finally
        {
            _loadingHostFolder = false;
        }
    }

    public ObservableCollection<SettingsTerminalRowViewModel> Rows { get; } = [];

    /// <summary>Raised when the page wants the import dialog; the window owns the actual dialog.</summary>
    public event Action? ImportRequested;

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

    [RelayCommand]
    private void Import() => ImportRequested?.Invoke();

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
