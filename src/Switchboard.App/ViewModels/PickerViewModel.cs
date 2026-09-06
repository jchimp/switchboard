using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.App.Services;
using Switchboard.Core.Registry;

namespace Switchboard.App.ViewModels;

/// <summary>One profile chip in the picker's Tab-cycled row.</summary>
public sealed partial class ProfileChipViewModel : ObservableObject
{
    public required string Name { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Backs the quick-launch picker (ADR-0017). Opens fully seeded from the quick-launch
/// defaults and the remembered directory, so a bare Enter reproduces the old
/// straight-to-launch behaviour one keystroke later.
/// </summary>
public sealed partial class PickerViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly QuickLaunchService _quick;
    private readonly WtProfileProvider _profiles;

    [ObservableProperty]
    private string _proposedName = string.Empty;

    [ObservableProperty]
    private string _directory = string.Empty;

    [ObservableProperty]
    private bool _keep;

    [ObservableProperty]
    private SolidColorBrush _swatch = Brushes.Transparent;

    public PickerViewModel(TerminalSession session, QuickLaunchService quick, WtProfileProvider profiles)
    {
        _session = session;
        _quick = quick;
        _profiles = profiles;
    }

    public ObservableCollection<ProfileChipViewModel> Profiles { get; } = [];

    public ProfileChipViewModel? SelectedProfile => Profiles.FirstOrDefault(p => p.IsSelected);

    /// <summary>Reseeds every field: fresh name/color, WT's current profiles, remembered directory.</summary>
    public void PrepareToOpen()
    {
        var q = _session.Registry.QuickLaunch;
        var seed = _quick.BuildDefinition();
        ProposedName = seed.Name;
        Keep = seed.Persistent;
        Directory = q.LastDirectory ?? string.Empty;
        Swatch = (SolidColorBrush)new BrushConverter().ConvertFromString(seed.Color)!;
        Swatch.Freeze();

        Profiles.Clear();
        foreach (var profile in _profiles.Load())
        {
            Profiles.Add(new ProfileChipViewModel { Name = profile.Name });
        }

        // No WT settings found (or none parsed): the registry default is still launchable.
        if (Profiles.Count == 0)
        {
            Profiles.Add(new ProfileChipViewModel { Name = q.DefaultProfile });
        }

        var preselect = Profiles.FirstOrDefault(p => string.Equals(p.Name, q.DefaultProfile, StringComparison.OrdinalIgnoreCase))
                        ?? Profiles[0];
        Select(preselect);
    }

    /// <summary>Tab / Shift+Tab: cycle the profile chips, wrapping.</summary>
    public void CycleProfile(int delta)
    {
        if (Profiles.Count == 0)
        {
            return;
        }

        var at = SelectedProfile is { } sel ? Profiles.IndexOf(sel) : 0;
        Select(Profiles[((at + delta) % Profiles.Count + Profiles.Count) % Profiles.Count]);
    }

    /// <summary>Enter: launch with the picked profile, directory, and keep flag. Remembers the directory.</summary>
    public async Task LaunchAsync()
    {
        var directory = Directory.Trim();
        await _quick.CreateAndLaunchAsync(
            profile: SelectedProfile?.Name,
            persistent: Keep,
            workingDirectory: directory.Length > 0 ? directory : null);

        if (directory.Length > 0)
        {
            await _session.RememberLastDirectoryAsync(directory);
        }
    }

    public void Select(ProfileChipViewModel chip)
    {
        foreach (var p in Profiles)
        {
            p.IsSelected = ReferenceEquals(p, chip);
        }

        OnPropertyChanged(nameof(SelectedProfile));
    }
}
