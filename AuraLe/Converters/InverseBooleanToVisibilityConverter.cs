using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AuraLe.Converters;

/// <summary>
/// bool 값을 반전하여 Visibility(True면 Collapsed, False면 Visible)로 변환
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
