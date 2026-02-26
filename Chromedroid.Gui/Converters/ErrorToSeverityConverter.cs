using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace Chromedroid.Gui;

public sealed class ErrorToSeverityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string)
            ? InfoBarSeverity.Informational
            : InfoBarSeverity.Error;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
