using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Switchboard.App.ViewModels;

/// <summary>One hue in a palette picker, with its selected ring state.</summary>
public sealed partial class ColorSwatchViewModel : ObservableObject
{
    public ColorSwatchViewModel(string hex)
    {
        Hex = hex;
        Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        Brush.Freeze();
    }

    public string Hex { get; }

    public SolidColorBrush Brush { get; }

    [ObservableProperty]
    private bool _isSelected;
}
