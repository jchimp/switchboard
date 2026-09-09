using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Core.Registry;

namespace Switchboard.App.ViewModels;

/// <summary>One unbound Windows Terminal window offered for import, with its proposed identity.</summary>
public sealed partial class ImportCandidateViewModel : ObservableObject
{
    public ImportCandidateViewModel(nint hwnd, string title, string name, string color)
    {
        Hwnd = hwnd;
        Title = title;
        _name = name;
        _color = color;
    }

    public nint Hwnd { get; }

    /// <summary>The live window title, shown read-only so the user can tell the windows apart.</summary>
    public string Title { get; }

    [ObservableProperty]
    private bool _selected = true;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Brush))]
    private string _color;

    [ObservableProperty]
    private string? _error;

    public SolidColorBrush Brush
    {
        get
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Color)!;
            brush.Freeze();
            return brush;
        }
    }
}

/// <summary>
/// "Import from running windows" (R27): adopt several already-running WT windows into the
/// registry at once, instead of the rail's one-at-a-time "Adopt as…".
/// </summary>
public sealed partial class ImportViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly QuickLaunchService _quick;

    public ImportViewModel(TerminalSession session, QuickLaunchService quick)
    {
        _session = session;
        _quick = quick;
    }

    public ObservableCollection<ImportCandidateViewModel> Candidates { get; } = [];

    /// <summary>Raised when the import committed and the dialog should close.</summary>
    public event Action? Finished;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? _summary;

    public bool IsEmpty => Candidates.Count == 0;

    /// <summary>Re-scans the desktop. This is an EnumWindows sweep, so it runs on demand only (R7).</summary>
    [RelayCommand]
    public void Refresh()
    {
        Candidates.Clear();

        // Names and hues must be unique across the batch as well as against the registry,
        // or the second window would collide with the first at commit time.
        var taken = _session.Registry.Terminals.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var claimed = _session.Registry.Terminals.ToList();

        foreach (var window in _session.UnboundWindows())
        {
            var name = NameSlug.FromTitle(window.Title, _session.Registry.QuickLaunch.NamePrefix, taken);
            taken.Add(name);

            var color = ColorPalette.NextUnused(claimed);
            claimed.Add(new TerminalDefinition { Name = name, Color = color, Profile = "placeholder" });
            Candidates.Add(new ImportCandidateViewModel(window.Hwnd, window.Title, name, color));
        }

        Summary = Candidates.Count == 0
            ? "No unbound Windows Terminal windows found."
            : $"{Candidates.Count} unbound window(s) found.";
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task Import()
    {
        var chosen = Candidates.Where(c => c.Selected).ToList();
        foreach (var candidate in chosen)
        {
            candidate.Error = null;
        }

        if (chosen.Count == 0)
        {
            Summary = "Nothing selected.";
            return;
        }

        // The window already exists, so this only borrows quick-launch's profile/shell defaults;
        // nothing is launched.
        var batch = chosen
            .Select(c => new ImportRequest(
                _quick.BuildDefinition() with { Name = c.Name, Color = c.Color },
                c.Hwnd))
            .ToList();

        var failures = await _session.ImportAsync(batch);
        foreach (var failure in failures)
        {
            if (chosen.FirstOrDefault(c => c.Name == failure.Name) is { } row)
            {
                row.Error = failure.Reason;
            }
        }

        if (failures.Count == 0)
        {
            Finished?.Invoke();
            return;
        }

        // Keep the dialog open so the failures stay readable; drop the rows that did land.
        var failed = failures.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var landed in chosen.Where(c => !failed.Contains(c.Name)).ToList())
        {
            Candidates.Remove(landed);
        }

        Summary = $"{chosen.Count - failures.Count} imported, {failures.Count} could not be.";
    }
}
