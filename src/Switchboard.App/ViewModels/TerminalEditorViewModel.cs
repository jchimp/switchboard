using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

/// <summary>
/// The right-hand editor on the Terminals page. Unlike the settings pages this commits on an
/// explicit Save (ADR-0018): a name change rebinds a live window (ADR-0014), which is not
/// something to do on every keystroke.
/// </summary>
public sealed partial class TerminalEditorViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly TerminalDefinition _original;

    /// <summary>Null for a row being added; otherwise the name to update, before any rename.</summary>
    private readonly string? _editingName;

    public TerminalEditorViewModel(TerminalSession session, TerminalDefinition definition, bool isNew)
    {
        _session = session;
        _original = definition;
        _editingName = isNew ? null : definition.Name;
        IsNew = isNew;

        _name = definition.Name;
        _tag = definition.Tag;
        _color = definition.Color;
        _profile = definition.Profile;
        _shell = definition.Shell;
        _distro = definition.Distro ?? string.Empty;
        _workingDirectory = definition.WorkingDirectory ?? string.Empty;
        _command = definition.Command ?? string.Empty;
        _jumpKey = definition.JumpKey;
        _lockTitle = definition.LockTitle;
        _persistent = definition.Persistent;

        foreach (var rule in definition.StatusRules)
        {
            Rules.Add(new StatusRuleRowViewModel(rule));
        }

        OnColorChanged(_color);
    }

    public bool IsNew { get; }

    /// <summary>The nine product hues, with the current one ringed.</summary>
    public IReadOnlyList<ColorSwatchViewModel> Palette { get; } =
        ColorPalette.Hues.Select(h => new ColorSwatchViewModel(h)).ToList();

    public static IReadOnlyList<ShellKind> Shells { get; } =
        [ShellKind.Pwsh, ShellKind.WindowsPowerShell, ShellKind.Cmd, ShellKind.Wsl];

    public ObservableCollection<StatusRuleRowViewModel> Rules { get; } = [];

    /// <summary>Raised after a successful save so the page can re-select the (possibly renamed) row.</summary>
    public event Action<string>? Saved;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _tag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Swatch))]
    private string _color;

    [ObservableProperty]
    private string _profile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWsl))]
    private ShellKind _shell;

    [ObservableProperty]
    private string _distro;

    [ObservableProperty]
    private string _workingDirectory;

    [ObservableProperty]
    private string _command;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JumpKeyText))]
    private int? _jumpKey;

    [ObservableProperty]
    private bool _lockTitle;

    [ObservableProperty]
    private bool _persistent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => Error is not null;

    public bool IsWsl => Shell == ShellKind.Wsl;

    public SolidColorBrush Swatch => Freeze(Color);

    /// <summary>The wt window name is the terminal name, always (invariant 4). Shown read-only.</summary>
    public string WindowName => Name;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(WindowName));

    partial void OnColorChanged(string value)
    {
        foreach (var swatch in Palette)
        {
            swatch.IsSelected = string.Equals(swatch.Hex, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Jump key as text so the box can be emptied. Anything that is not 1–9 clears the key
    /// rather than erroring: Validate() would reject it on Save, which is a poor way to
    /// learn that a stray character is not a jump key.
    /// </summary>
    public string JumpKeyText
    {
        get => JumpKey?.ToString() ?? string.Empty;
        set => JumpKey = int.TryParse(value, out var digit) && digit is >= 1 and <= 9 ? digit : null;
    }

    [RelayCommand]
    private void SelectColor(ColorSwatchViewModel swatch) => Color = swatch.Hex;

    [RelayCommand]
    private void AddRule() => Rules.Add(new StatusRuleRowViewModel(
        new StatusRule { State = TerminalState.Working, Pattern = string.Empty }));

    [RelayCommand]
    private void RemoveRule(StatusRuleRowViewModel rule) => Rules.Remove(rule);

    [RelayCommand]
    private async Task Save()
    {
        Error = null;

        if (Rules.FirstOrDefault(r => r.HasError) is { } bad)
        {
            Error = $"Rule '{bad.Pattern}': {bad.Error}";
            return;
        }

        var updated = Build();
        try
        {
            if (_editingName is { } existing)
            {
                await _session.UpdateTerminalAsync(existing, updated);
            }
            else
            {
                await _session.AddTerminalAsync(updated);
            }

            Saved?.Invoke(updated.Name);
        }
        catch (ArgumentException ex)
        {
            // Validate() and the duplicate-name check both arrive here; show them inline.
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private Task Launch() => _session.ActivateAsync(_original.Name);

    private TerminalDefinition Build() => _original with
    {
        Name = Name.Trim(),
        Tag = Tag.Trim(),
        Color = Color,
        Profile = Profile.Trim(),
        Shell = Shell,
        Distro = Blank(Distro),
        WorkingDirectory = Blank(WorkingDirectory),
        Command = Blank(Command),
        JumpKey = JumpKey,
        LockTitle = LockTitle,
        Persistent = Persistent,
        StatusRules = Rules.Select(r => r.ToRule()).ToList(),
    };

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
