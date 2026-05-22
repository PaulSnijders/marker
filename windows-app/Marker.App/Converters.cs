using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Marker.App;

/// <summary>Converts a "#RRGGBB" hex string into a frozen <see cref="SolidColorBrush"/>.</summary>
public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                return brush;
            }
            catch
            {
                // Fall through to a neutral brush on a malformed value.
            }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
