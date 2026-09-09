using System.Globalization;
using System.Windows.Data;

namespace Switchboard.App.Settings;

/// <summary>Inverts a bool. Used to grey out settings another setting has made inert.</summary>
public sealed class NotBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}
