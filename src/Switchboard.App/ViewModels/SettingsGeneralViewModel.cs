using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Windows;

namespace Switchboard.App.ViewModels;

/// <summary>
/// The General page: where Switchboard's terminals come from (the terminal host folder,
/// ADR-0021) and importing the installed WT's look into it (ADR-0022). Moved off the
/// Terminals page so that page is the list and its editor, with the room to use them.
/// </summary>
public sealed partial class SettingsGeneralViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly SettingsApplyQueue _queue;
    private readonly WtAppearanceImporter _appearance;
    private bool _loadingHostFolder;
    private string? _appearanceFolder;

    public SettingsGeneralViewModel(TerminalSession session, SettingsApplyQueue queue, WtAppearanceImporter appearance)
    {
        _session = session;
        _queue = queue;
        _appearance = appearance;
        _session.RegistryChanged += OnRegistryChanged;
        _appearanceFolder = session.Registry.TerminalHost.Folder;
        LoadHostFolder();
        RefreshAppearance();
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

    /// <summary>
    /// The window calls this when it is shown: the host WT may have created its settings.json,
    /// or either file been edited, since the page last looked.
    /// </summary>
    public void RefreshFromDisk() => RefreshAppearance();

    partial void OnTerminalHostFolderChanged(string value)
    {
        if (_loadingHostFolder)
        {
            return;
        }

        var folder = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        _queue.Queue(registry => registry with { TerminalHost = registry.TerminalHost with { Folder = folder } });
    }

    /// <summary>One-shot, user-triggered: it overwrites the host's appearance (ADR-0022).</summary>
    [RelayCommand(CanExecute = nameof(CanImportAppearance))]
    private async Task ImportAppearance()
    {
        AppearanceResult = await _appearance.ImportAsync();
        RefreshAppearance();
    }

    /// <summary>Re-reads both WT files for the appearance import. Never on a timer.</summary>
    private void RefreshAppearance() => AppearanceState = _appearance.CurrentState();

    private void OnRegistryChanged()
    {
        // Our own save echoes back; reloading it would rewrite the box mid-typing.
        if (!_queue.IsApplyingOwnEdit)
        {
            LoadHostFolder();
        }

        // A different host makes an old "Imported…" line describe the wrong file.
        if (_session.Registry.TerminalHost.Folder != _appearanceFolder)
        {
            _appearanceFolder = _session.Registry.TerminalHost.Folder;
            AppearanceResult = null;
            RefreshAppearance();
        }
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
}
