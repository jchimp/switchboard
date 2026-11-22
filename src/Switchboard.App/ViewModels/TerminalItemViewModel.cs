using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

/// <summary>One strip segment.</summary>
public sealed partial class TerminalItemViewModel : ObservableObject
{
    public TerminalItemViewModel(TerminalDefinition definition)
    {
        Definition = definition;
        Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(definition.Color)!;
        Brush.Freeze();
        // Dormant() always returns #RRGGBB, so the converter can't return null.
        DormantBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(ColorPalette.Dormant(definition.Color))!;
        DormantBrush.Freeze();
    }

    public TerminalDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Tag => Definition.DisplayTag;

    public SolidColorBrush Brush { get; }

    public SolidColorBrush DormantBrush { get; }

    /// <summary>Segment fill: the terminal's color, or its greyed-out form while Closed.</summary>
    public SolidColorBrush Fill => IsClosed ? DormantBrush : Brush;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClosed), nameof(Fill), nameof(WantsAttention), nameof(Opacity), nameof(ToolTip))]
    private TerminalState _state = TerminalState.Closed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GlowRadius))]
    private bool _isActive;

    // Trigger setters can't reach a DropShadowEffect by TargetName (it's a Freezable,
    // not a template element), so the glow is driven from here instead.
    public double GlowRadius => IsActive ? 14 : 0;

    public bool IsClosed => State == TerminalState.Closed;

    public bool WantsAttention => State == TerminalState.Attention;

    public double Opacity => State switch
    {
        // Fill already greys a Closed segment; a deep fade on top would lose it against the strip.
        TerminalState.Closed => 0.6,
        TerminalState.Idle => 0.75,
        _ => 1.0,
    };

    public string ToolTip => $"{Name} · {Definition.Profile}" +
                             (Definition.WorkingDirectory is { } d ? $" · {d}" : string.Empty) +
                             $"\n{State}" + (Definition.JumpKey is { } k ? $" · Ctrl+Alt+{k}" : string.Empty);
}
