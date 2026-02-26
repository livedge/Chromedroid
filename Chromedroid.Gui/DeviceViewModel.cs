using System.Collections.ObjectModel;
using System.Reflection;
using AdvancedSharpAdbClient.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chromedroid.Gui;

public sealed record BrowserStatusItem(string DisplayName, string PackageName);

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

    public string DisplayName => Nickname ?? Model ?? Serial;

    public string? DeviceInfo =>
        Manufacturer is not null && AndroidVersion is not null
            ? $"{Manufacturer} \u00b7 Android {AndroidVersion}"
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
            RunningBrowsers.Clear();
            return;
        }

        string output;
        try
        {
            output = await _bridge.ExecuteShellAsync(DeviceData, "ps -A", ct);
        }
        catch
        {
            RunningBrowsers.Clear();
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

        var detected = KnownBrowsers
            .Where(b => runningPackages.Contains(b.PackageName))
            .Select(b => new BrowserStatusItem(b.DisplayName, b.PackageName))
            .ToList();

        // Reconcile: remove stale, add new
        for (var i = RunningBrowsers.Count - 1; i >= 0; i--)
        {
            if (!detected.Any(d => d.PackageName == RunningBrowsers[i].PackageName))
                RunningBrowsers.RemoveAt(i);
        }

        foreach (var item in detected)
        {
            if (!RunningBrowsers.Any(b => b.PackageName == item.PackageName))
                RunningBrowsers.Add(item);
        }
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
