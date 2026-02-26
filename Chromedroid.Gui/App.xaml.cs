using System.Windows;
using Wpf.Ui.Appearance;

namespace Chromedroid.Gui;

public partial class App : Application
{
    private AdbBridge? _bridge;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        ApplicationThemeManager.ApplySystemTheme();

        try
        {
            _bridge = AdbBridge.Create();
        }
        catch (AdbException ex)
        {
            MessageBox.Show(
                $"Failed to start ADB server:\n{ex.Message}",
                "Chromedroid",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var nicknameStore = new NicknameStore();
        var vm = new MainViewModel(_bridge, nicknameStore);
        var window = new MainWindow { DataContext = vm };
        window.Show();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        if (_bridge is not null)
            await _bridge.DisposeAsync();
    }
}
