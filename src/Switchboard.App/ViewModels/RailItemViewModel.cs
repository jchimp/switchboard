using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

/// <summary>One rail row: name, distro · cwd sub-line, jump-key badge, state.</summary>
public sealed partial class RailItemViewModel : ObservableObject
{
    public RailItemViewModel(TerminalDefinition definition, int position)
    {
        Definition = definition;
        Position = position;
        Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(definition.Color)!;
        Brush.Freeze();
    }

    public TerminalDefinition Definition { get; }

    /// <summary>1-based registry position; the jump fallback when no explicit key is set.</summary>
    public int Position { get; }

    public string Name => Definition.Name;

    public SolidColorBrush Brush { get; }

    public string SubLine => string.Join(" · ", Parts());

    /// <summary>Digit shown in the key badge, mirroring ActivateByJumpKeyAsync's resolution.</summary>
    public string JumpKeyText => (Definition.JumpKey ?? (Position <= 9 ? Position : null))?.ToString() ?? string.Empty;

    public bool HasJumpKey => JumpKeyText.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClosed), nameof(WantsAttention), nameof(RowOpacity), nameof(IsBusy))]
    private TerminalState _state = TerminalState.Closed;

    [ObservableProperty]
    private bool _isActive;

    public bool IsClosed => State == TerminalState.Closed;

    public bool WantsAttention => State == TerminalState.Attention;

    public bool IsBusy => State is TerminalState.Working or TerminalState.Attention;

    public double RowOpacity => IsClosed ? 0.45 : 1.0;

    // No shell name: it said "pwsh" on almost every card and told the user nothing the
    // terminal's own name didn't. Distro and folder are what tell two terminals apart.
    private IEnumerable<string> Parts()
    {
        if (Definition.Distro is { Length: > 0 } distro)
        {
            yield return distro;
        }

        if (Definition.WorkingDirectory is { Length: > 0 } dir)
        {
            yield return dir;
        }
    }
}
