using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Switchboard.Core.Registry;
using Switchboard.Windows;

namespace Switchboard.App.Rail;

/// <summary>
/// The rename / recolor / adopt-as-new editor (ADR-0014): the one window in the product
/// that takes focus, and only because the user clicked an edit affordance. Captures the
/// foreground window on open and hands focus back on close, so the round-trip is invisible.
/// The commit callback returns a user-facing error to show inline, or null to close.
/// </summary>
public partial class RailEditPopup : Window
{
    private readonly Func<string, string, Task<string?>> _commit;
    private readonly nint _restoreTo;
    private string _color;

    public RailEditPopup(string title, string initialName, string initialColor, Func<string, string, Task<string?>> commit)
    {
        _commit = commit;
        _color = initialColor;
        _restoreTo = WindowFocuser.CurrentForeground();
        InitializeComponent();
        TitleText.Text = title;
        NameBox.Text = initialName;
        BuildSwatches();
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
        Closed += (_, _) => WindowFocuser.Focus(_restoreTo);
    }

    private void BuildSwatches()
    {
        foreach (var hue in ColorPalette.Hues)
        {
            // Palette entries are compile-time #RRGGBB constants; the converter cannot fail.
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hue)!;
            brush.Freeze();
            var swatch = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 6, 6),
                Background = brush,
                BorderThickness = new Thickness(2),
                BorderBrush = SameColor(hue, _color) ? Brushes.White : Brushes.Transparent,
                Cursor = Cursors.Hand,
                Tag = hue,
            };
            swatch.MouseLeftButtonUp += (_, _) => SelectColor(hue);
            Swatches.Children.Add(swatch);
        }
    }

    private void SelectColor(string hue)
    {
        _color = hue;
        foreach (var child in Swatches.Children)
        {
            if (child is Border { Tag: string tag } swatch)
            {
                swatch.BorderBrush = SameColor(tag, hue) ? Brushes.White : Brushes.Transparent;
            }
        }
    }

    private static bool SameColor(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var error = await _commit(NameBox.Text.Trim(), _color);
            if (error is null)
            {
                Close();
                return;
            }

            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
