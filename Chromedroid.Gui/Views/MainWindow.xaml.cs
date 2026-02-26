using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Chromedroid.Gui;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as MainViewModel)?.Dispose();
        base.OnClosed(e);
    }

    private void DeviceCard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DeviceViewModel device })
        {
            var dialog = new NicknameDialog(device.Model, device.Nickname)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                device.SetNickname(dialog.ResultNickname);
            }
        }
    }
}
