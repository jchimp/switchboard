using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Core.Registry;

namespace Switchboard.App.ViewModels;

/// <summary>The settings window's nav pages (ADR-0018 — no Appearance page: nothing backs it).</summary>
public enum SettingsPage
{
    General,
    Terminals,
    Strip,
    Hotkeys,
    Status,
}

/// <summary>Nav labels for <see cref="SettingsPage"/>; the enum names are not all display-ready.</summary>
public sealed class SettingsPageNameConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value switch
        {
            SettingsPage.General => "General",
            SettingsPage.Terminals => "Terminals",
            SettingsPage.Strip => "Edge strip",
            SettingsPage.Hotkeys => "Hotkeys",
            SettingsPage.Status => "Status rules",
            _ => string.Empty,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Shell for the settings window: which page is showing, and the file the whole thing edits.
/// The pages own their own state; this only routes between them.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsApplyQueue _queue;
    private readonly TerminalSession _session;

    public SettingsViewModel(
        SettingsGeneralViewModel general,
        SettingsTerminalsViewModel terminals,
        SettingsStripViewModel strip,
        SettingsStatusViewModel status,
        SettingsHotkeysViewModel hotkeys,
        SettingsApplyQueue queue,
        TerminalSession session)
    {
        General = general;
        Terminals = terminals;
        Strip = strip;
        Status = status;
        Hotkeys = hotkeys;
        _queue = queue;
        _session = session;
    }

    public SettingsGeneralViewModel General { get; }

    public SettingsTerminalsViewModel Terminals { get; }

    public SettingsStripViewModel Strip { get; }

    public SettingsStatusViewModel Status { get; }

    public SettingsHotkeysViewModel Hotkeys { get; }

    /// <summary>Shown in the nav footer, the way the mockup labels the file being edited.</summary>
    public string RegistryPath => RegistryPaths.Default;

    /// <summary>Nav order. Bound as a selection list so arrow keys work and the page is the state.</summary>
    public static IReadOnlyList<SettingsPage> Pages { get; } =
        [SettingsPage.General, SettingsPage.Terminals, SettingsPage.Strip, SettingsPage.Hotkeys, SettingsPage.Status];

    // Opens on Terminals, not the first nav item: the list and its editor are what the window
    // is mostly for.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneral), nameof(IsTerminals), nameof(IsStrip), nameof(IsHotkeys), nameof(IsStatus))]
    private SettingsPage _page = SettingsPage.Terminals;

    public bool IsGeneral => Page == SettingsPage.General;

    public bool IsTerminals => Page == SettingsPage.Terminals;

    public bool IsStrip => Page == SettingsPage.Strip;

    public bool IsHotkeys => Page == SettingsPage.Hotkeys;

    public bool IsStatus => Page == SettingsPage.Status;

    /// <summary>Writes any debounced edit immediately; called as the window hides.</summary>
    public Task FlushAsync() => _queue.FlushAsync();

    /// <summary>The size and split to open with, clamped so a stale value stays usable.</summary>
    public SettingsWindowLayout Layout => SettingsBounds.Clamp(_session.Registry.SettingsWindow);

    /// <summary>
    /// Queues the size and split the user left, saved with the hide-time flush. Skipped when
    /// nothing moved, so just opening and closing Settings doesn't write the file.
    /// </summary>
    public void RememberLayout(SettingsWindowLayout layout)
    {
        var clamped = SettingsBounds.Clamp(layout);
        if (clamped != Layout)
        {
            _queue.Queue(registry => registry with { SettingsWindow = clamped });
        }
    }
}
