using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AdvancedSharpAdbClient.Models;

namespace Chromedroid.Gui;

public sealed class DeviceStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DeviceState state
            ? state switch
            {
                DeviceState.Online => Brushes.Green,
                DeviceState.Offline => Brushes.Gray,
                DeviceState.Unauthorized => Brushes.Orange,
                DeviceState.BootLoader => Brushes.DodgerBlue,
                _ => Brushes.Red,
            }
            : Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
