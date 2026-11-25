using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Windows;

namespace Switchboard.App.ViewModels;

/// <summary>
/// The General page: where Switchboard's terminals come from (the terminal host folder,
/// ADR-0021), importing the installed WT's look into it (ADR-0022), starting at sign-in
/// (ADR-0026) and the About facts. Moved off the Terminals page so that page is the list
/// and its editor, with the room to use them.
/// </summary>
public sealed partial class SettingsGeneralViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly SettingsApplyQueue _queue;
    private readonly WtAppearanceImporter _appearance;
    private readonly StartupShortcut _startup;
    private readonly string? _exePath = Environment.ProcessPath;
    private bool _loadingHostFolder;
    private bool _loadingStartup;
    private string? _appearanceFolder;

    public SettingsGeneralViewModel(
        TerminalSession session,
        SettingsApplyQueue queue,
        WtAppearanceImporter appearance,
        StartupShortcut startup)
    {
        _session = session;
        _queue = queue;
        _appearance = appearance;
        _startup = startup;
        _session.RegistryChanged += OnRegistryChanged;
        _appearanceFolder = session.Registry.TerminalHost.Folder;
        LoadHostFolder();
        RefreshAppearance();
        RefreshStartup();
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
    /// A Startup-folder shortcut exists (ADR-0026). Not a registry setting: the shortcut is the
    /// state, so this writes or deletes the file at once instead of going through the queue.
    /// </summary>
    [ObservableProperty]
    private bool _startAtSignIn;

    /// <summary>Why the checkbox may not do what it says: a failure, Windows' switch, or another exe.</summary>
    [ObservableProperty]
    private string? _startupWarning;

    /// <summary>Release number, without the commit hash the SDK appends.</summary>
    public string Version { get; } = ReadVersion();

    public string RegistryFilePath => RegistryPaths.Default;

    public string LogFolder => FileLoggerProvider.DefaultDirectory;

    /// <summary>
    /// The window calls this when it is shown: the host WT may have created its settings.json,
    /// either file been edited, or the Startup shortcut changed since the page last looked.
    /// </summary>
    public void RefreshFromDisk()
    {
        RefreshAppearance();
        RefreshStartup();
    }

    partial void OnStartAtSignInChanged(bool value)
    {
        if (_loadingStartup)
        {
            return;
        }

        string? error = null;
        if (value && (_exePath is null || !_startup.Enable(_exePath)))
        {
            error = $"Could not create {StartupShortcut.LinkPath}. The log has the reason.";
        }
        else if (!value && !_startup.Disable())
        {
            error = $"Could not remove {StartupShortcut.LinkPath}. The log has the reason.";
        }

        RefreshStartup(error);
    }

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

    /// <summary>Re-reads the shortcut, so the box shows the file, not what was last clicked.</summary>
    private void RefreshStartup(string? error = null)
    {
        var status = _startup.Read(_exePath ?? string.Empty);
        _loadingStartup = true;
        try
        {
            StartAtSignIn = status.Exists;
        }
        finally
        {
            _loadingStartup = false;
        }

        StartupWarning = error ?? DescribeProblem(status);
    }

    private static string? DescribeProblem(StartupShortcutStatus status)
    {
        if (!status.Exists)
        {
            return null;
        }

        if (status.TurnedOffInWindows)
        {
            return "The shortcut is there, but Windows has it turned off. Turn Switchboard on in Windows Settings > Apps > Startup.";
        }

        return status.PointsAtThisExe
            ? null
            : $"The shortcut starts {status.Target ?? "a program Switchboard cannot read"}, not this copy. Turn the box off and on to point it here.";
    }

    private static string ReadVersion()
    {
        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return version?.Split('+')[0] ?? "unknown";
    }

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
