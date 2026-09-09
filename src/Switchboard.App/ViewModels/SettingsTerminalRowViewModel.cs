using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

/// <summary>One row of the settings Terminals table: swatch, name, tag, profile, live status, key.</summary>
public sealed partial class SettingsTerminalRowViewModel : ObservableObject
{
    public SettingsTerminalRowViewModel(TerminalDefinition definition, int position)
    {
        Definition = definition;
        Position = position;
        Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(definition.Color)!;
        Brush.Freeze();
    }

    public TerminalDefinition Definition { get; }

    /// <summary>1-based registry position — the strip's order, and the jump-key fallback.</summary>
    public int Position { get; }

    public string Name => Definition.Name;

    public string Tag => Definition.DisplayTag;

    public string Profile => Definition.Profile;

    public SolidColorBrush Brush { get; }

    public string JumpKeyText => (Definition.JumpKey ?? (Position <= 9 ? Position : null))?.ToString() ?? string.Empty;

    public bool HasJumpKey => JumpKeyText.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(IsClosed), nameof(StateDotOpacity))]
    private TerminalState _state = TerminalState.Closed;

    public bool IsClosed => State == TerminalState.Closed;

    /// <summary>The status dot carries the terminal's own hue; state shows as how bright it burns.</summary>
    public double StateDotOpacity => State switch
    {
        TerminalState.Working => 1.0,
        TerminalState.Attention => 1.0,
        TerminalState.Idle => 0.5,
        _ => 0.15,
    };

    public string StateText => State switch
    {
        TerminalState.Working => "Working",
        TerminalState.Attention => "Attention",
        TerminalState.Idle => "Idle",
        _ => "Closed",
    };
}
