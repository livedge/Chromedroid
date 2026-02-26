using System.Collections.ObjectModel;
using System.Reflection;
using AdvancedSharpAdbClient.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chromedroid.Gui;

public sealed partial class DeviceViewModel : ObservableObject
{
    private static readonly IReadOnlyList<ChromeBrowser> KnownBrowsers = GetKnownBrowsers();

    private readonly AdbBridge _bridge;
    private readonly NicknameStore _nicknameStore;
    private bool _hasDeviceInfo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _serial = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _model = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IdentifyCommand))]
    private DeviceState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _nickname;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceInfo))]
    private string? _manufacturer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceInfo))]
    private string? _androidVersion;

    [ObservableProperty]
    private string? _sdkLevel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IdentifyCommand))]
    private bool _isIdentifying;

    [ObservableProperty]
    private BrowserStatusItem? _selectedBrowser;

    public string DisplayName => Nickname ?? Model ?? Serial;

    public string? DeviceInfo =>
        Manufacturer is not null && AndroidVersion is not null
            ? $"{Manufacturer} · Android {AndroidVersion}"
            : null;

    public ObservableCollection<BrowserStatusItem> RunningBrowsers { get; } = [];

    public DeviceData DeviceData { get; private set; } = null!;

    public DeviceViewModel(AdbBridge bridge, NicknameStore nicknameStore, DeviceData device)
    {
        _bridge = bridge;
        _nicknameStore = nicknameStore;
        Update(device);
        _nickname = nicknameStore.GetNickname(device.Serial);
    }

    public void Update(DeviceData device)
    {
        DeviceData = device;
        Serial = device.Serial;
        Model = string.IsNullOrWhiteSpace(device.Model) ? device.Serial : device.Model;

        // Reset device info when state transitions away from Online
        if (State != device.State && device.State != DeviceState.Online)
            _hasDeviceInfo = false;

        State = device.State;
    }

    public void SetNickname(string? nickname)
    {
        Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname;
        _nicknameStore.SetNickname(Serial, Nickname);
    }

    [RelayCommand]
    private async Task SelectBrowserAsync(BrowserStatusItem? browser)
    {
        if (browser is not null && browser == SelectedBrowser)
        {
            // Toggle off
            await DeselectBrowserAsync();
        }
        else
        {
            // Deselect old, select new
            await DeselectBrowserAsync();
            if (browser is not null)
            {
                browser.IsSelected = true;
                SelectedBrowser = browser;
            }
        }
    }

    public async Task RefreshOpenPagesAsync(CancellationToken ct = default)
    {
        if (SelectedBrowser is not null)
            await SelectedBrowser.RefreshPagesAsync(DeviceData, ct);
    }

    private async Task DeselectBrowserAsync()
    {
        if (SelectedBrowser is not null)
        {
            SelectedBrowser.IsSelected = false;
            await SelectedBrowser.DisposeAsync();
            SelectedBrowser = null;
        }
    }

    public async Task FetchDeviceInfoAsync(CancellationToken ct = default)
    {
        if (State != DeviceState.Online || _hasDeviceInfo) return;

        try
        {
            var manufacturer = await _bridge.ExecuteShellAsync(DeviceData, "getprop ro.product.manufacturer", ct);
            var androidVersion = await _bridge.ExecuteShellAsync(DeviceData, "getprop ro.build.version.release", ct);
            var sdkLevel = await _bridge.ExecuteShellAsync(DeviceData, "getprop ro.build.version.sdk", ct);

            Manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? null : CapitalizeFirst(manufacturer);
            AndroidVersion = string.IsNullOrWhiteSpace(androidVersion) ? null : androidVersion;
            SdkLevel = string.IsNullOrWhiteSpace(sdkLevel) ? null : sdkLevel;

            _hasDeviceInfo = true;
        }
        catch
        {
            // Best-effort; will retry on next refresh
        }
    }

    public async Task DetectRunningBrowsersAsync(CancellationToken ct = default)
    {
        if (State != DeviceState.Online)
        {
            await ClearBrowsersAsync();
            return;
        }

        string output;
        try
        {
            output = await _bridge.ExecuteShellAsync(DeviceData, "ps -A", ct);
        }
        catch
        {
            await ClearBrowsersAsync();
            return;
        }

        var runningPackages = new HashSet<string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // ps -A output: USER PID PPID VSZ RSS WCHAN ADDR S NAME
            // The last column is the process name (package name for apps).
            var trimmed = line.TrimEnd();
            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace < 0) continue;
            var processName = trimmed[(lastSpace + 1)..];
            runningPackages.Add(processName);
        }

        var detectedPackages = KnownBrowsers
            .Where(b => runningPackages.Contains(b.PackageName))
            .ToList();

        // Reconcile: remove stale, add new
        for (var i = RunningBrowsers.Count - 1; i >= 0; i--)
        {
            var item = RunningBrowsers[i];
            if (!detectedPackages.Any(d => d.PackageName == item.PackageName))
            {
                if (SelectedBrowser == item)
                    await DeselectBrowserAsync();

                await item.DisposeAsync();
                RunningBrowsers.RemoveAt(i);
            }
        }

        foreach (var browser in detectedPackages)
        {
            if (!RunningBrowsers.Any(b => b.PackageName == browser.PackageName))
                RunningBrowsers.Add(new BrowserStatusItem(_bridge, browser));
        }
    }

    private async Task ClearBrowsersAsync()
    {
        await DeselectBrowserAsync();

        foreach (var item in RunningBrowsers)
            await item.DisposeAsync();

        RunningBrowsers.Clear();
    }

    private bool CanIdentify => State == DeviceState.Online && !IsIdentifying;

    [RelayCommand(CanExecute = nameof(CanIdentify))]
    private async Task IdentifyAsync()
    {
        IsIdentifying = true;
        try
        {
            await _bridge.FlashScreenAsync(DeviceData);
        }
        catch
        {
            // Best-effort; ignore failures
        }
        finally
        {
            IsIdentifying = false;
        }
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static IReadOnlyList<ChromeBrowser> GetKnownBrowsers()
    {
        return typeof(ChromeBrowser)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(ChromeBrowser))
            .Select(p => (ChromeBrowser)p.GetValue(null)!)
            .ToList();
    }
}
