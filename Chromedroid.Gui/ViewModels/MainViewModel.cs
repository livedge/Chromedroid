using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chromedroid.Gui;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AdbBridge _bridge;
    private readonly NicknameStore _nicknameStore;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    [ObservableProperty]
    private string _statusText = "Starting...";

    [ObservableProperty]
    private string _errorText = string.Empty;

    [ObservableProperty]
    private DeviceViewModel? _selectedDevice;

    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    public MainViewModel(AdbBridge bridge, NicknameStore nicknameStore)
    {
        _bridge = bridge;
        _nicknameStore = nicknameStore;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            ErrorText = string.Empty;

            var devices = await _bridge.GetDevicesAsync();
            var serials = new HashSet<string>(devices.Select(d => d.Serial));

            // Remove stale devices
            for (var i = Devices.Count - 1; i >= 0; i--)
            {
                if (!serials.Contains(Devices[i].Serial))
                    Devices.RemoveAt(i);
            }

            // Add or update devices
            foreach (var device in devices)
            {
                var existing = Devices.FirstOrDefault(d => d.Serial == device.Serial);
                if (existing is not null)
                {
                    existing.Update(device);
                }
                else
                {
                    Devices.Add(new DeviceViewModel(_bridge, _nicknameStore, device));
                }
            }

            StatusText = $"{Devices.Count} device(s) connected";

            // Fetch device info and detect running browsers
            foreach (var vm in Devices)
            {
                await vm.FetchDeviceInfoAsync();
                await vm.DetectRunningBrowsersAsync();
            }

            // Refresh open pages for selected browser
            if (SelectedDevice?.SelectedBrowser is not null)
                await SelectedDevice.RefreshOpenPagesAsync();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
    }
}
