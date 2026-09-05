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
    }

    public TerminalDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Tag => Definition.DisplayTag;

    public SolidColorBrush Brush { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClosed), nameof(WantsAttention), nameof(Opacity), nameof(ToolTip))]
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
        TerminalState.Closed => 0.28,
        TerminalState.Idle => 0.75,
        _ => 1.0,
    };

    public string ToolTip => $"{Name} · {Definition.Profile}" +
                             (Definition.WorkingDirectory is { } d ? $" · {d}" : string.Empty) +
                             $"\n{State}" + (Definition.JumpKey is { } k ? $" · Ctrl+Alt+{k}" : string.Empty);
}
