using System.Net;
using System.Net.Sockets;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;

namespace Chromedroid;

/// <summary>
/// Main public API for ADB communication — device discovery, browser launch, and CDP forwarding.
/// </summary>
public sealed class AdbBridge : IAsyncDisposable
{
    private readonly AdbClient _client;
    private readonly List<CdpForward> _forwards = [];
    private bool _disposed;

    private AdbBridge(AdbClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Creates an <see cref="AdbBridge"/> instance, ensuring the ADB server is running.
    /// </summary>
    /// <param name="adbPath">
    /// Path to the <c>adb</c> executable. Defaults to <c>"adb"</c> (expects it on PATH).
    /// </param>
    public static AdbBridge Create(string? adbPath = null)
    {
        var client = new AdbClient();
        var server = new AdbServer(client);

        try
        {
            server.StartServer(adbPath ?? "adb", restartServerIfNewer: false);
        }
        catch (Exception ex)
        {
            throw new AdbException("Failed to start ADB server.", ex);
        }

        return new AdbBridge(client);
    }

    /// <summary>Returns all connected devices regardless of state.</summary>
    public async Task<IReadOnlyList<DeviceData>> GetDevicesAsync(CancellationToken ct = default)
    {
        try
        {
            var devices = await _client.GetDevicesAsync(ct).ConfigureAwait(false);
            return devices.ToList();
        }
        catch (Exception ex) when (ex is not AdbException and not OperationCanceledException)
        {
            throw new AdbException("Failed to enumerate devices.", ex);
        }
    }

    /// <summary>Returns only devices in the <see cref="DeviceState.Online"/> state.</summary>
    public async Task<IReadOnlyList<DeviceData>> GetOnlineDevicesAsync(CancellationToken ct = default)
    {
        var devices = await GetDevicesAsync(ct).ConfigureAwait(false);
        return devices.Where(d => d.State == DeviceState.Online).ToList();
    }

    /// <summary>
    /// Launches a Chromium browser on the device with automation-friendly flags.
    /// </summary>
    /// <param name="device">Target device.</param>
    /// <param name="browser">Browser to launch. Defaults to <see cref="ChromeBrowser.Chrome"/>.</param>
    /// <param name="url">Optional URL to navigate to on launch.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task LaunchBrowserAsync(
        DeviceData device,
        ChromeBrowser? browser = null,
        Uri? url = null,
        CancellationToken ct = default)
    {
        browser ??= ChromeBrowser.Chrome;

        try
        {
            // Write Chrome command-line flags for automation
            var flagsFile = $"/data/local/tmp/{browser.PackageName}-command-line";
            await ExecuteShellAsync(device,
                    $"echo '_ --disable-fre --no-default-browser-check --no-first-run' > {flagsFile}", ct)
                .ConfigureAwait(false);

            // Force-stop any existing instance
            await ExecuteShellAsync(device, $"am force-stop {browser.PackageName}", ct)
                .ConfigureAwait(false);

            // Launch the browser
            var intent = $"-n {browser.PackageName}/{browser.MainActivity}";
            if (url is not null)
                intent += $" -a android.intent.action.VIEW -d '{url}'";

            await ExecuteShellAsync(device, $"am start {intent}", ct).ConfigureAwait(false);

            // Brief delay for the DevTools socket to appear
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not AdbException and not OperationCanceledException)
        {
            throw new AdbException($"Failed to launch {browser.DisplayName}.", ex);
        }
    }

    /// <summary>
    /// Sets up a TCP port forward from localhost to the browser's DevTools Unix socket.
    /// </summary>
    /// <param name="device">Target device.</param>
    /// <param name="browser">Browser whose socket to forward. Defaults to <see cref="ChromeBrowser.Chrome"/>.</param>
    /// <param name="localPort">Local TCP port. Use 0 to auto-assign a free port.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CdpForward"/> that removes the forward on dispose.</returns>
    public async Task<CdpForward> ForwardCdpAsync(
        DeviceData device,
        ChromeBrowser? browser = null,
        int localPort = 0,
        CancellationToken ct = default)
    {
        browser ??= ChromeBrowser.Chrome;

        try
        {
            if (localPort == 0)
                localPort = FindFreePort();

            var local = $"tcp:{localPort}";
            var remote = $"localabstract:{browser.DevToolsSocketName}";

            await _client.CreateForwardAsync(device, local, remote, true, ct)
                .ConfigureAwait(false);

            var forward = new CdpForward(_client, device, localPort, browser.DevToolsSocketName);
            _forwards.Add(forward);
            return forward;
        }
        catch (Exception ex) when (ex is not AdbException and not OperationCanceledException)
        {
            throw new AdbException(
                $"Failed to forward CDP socket '{browser.DevToolsSocketName}'.", ex);
        }
    }

    /// <summary>
    /// Briefly sets the device screen to maximum brightness for visual identification,
    /// then restores the original brightness and mode.
    /// </summary>
    /// <param name="device">Target device.</param>
    /// <param name="durationMs">How long to keep max brightness, in milliseconds.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task FlashScreenAsync(DeviceData device, int durationMs = 1000, CancellationToken ct = default)
    {
        string originalBrightness;
        string originalMode;

        try
        {
            originalBrightness = await ExecuteShellAsync(device, "settings get system screen_brightness", ct)
                .ConfigureAwait(false);
            originalMode = await ExecuteShellAsync(device, "settings get system screen_brightness_mode", ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not AdbException and not OperationCanceledException)
        {
            throw new AdbException("Failed to read screen brightness settings.", ex);
        }

        try
        {
            // Wake the screen (no-op if already on)
            await ExecuteShellAsync(device, "input keyevent 224", ct).ConfigureAwait(false);

            // Force manual brightness and set to max
            await ExecuteShellAsync(device, "settings put system screen_brightness_mode 0", ct)
                .ConfigureAwait(false);
            await ExecuteShellAsync(device, "settings put system screen_brightness 255", ct)
                .ConfigureAwait(false);

            await Task.Delay(durationMs, ct).ConfigureAwait(false);
        }
        finally
        {
            // Restore original settings regardless of cancellation or error
            try
            {
                await ExecuteShellAsync(device, $"settings put system screen_brightness {originalBrightness}", CancellationToken.None)
                    .ConfigureAwait(false);
                await ExecuteShellAsync(device, $"settings put system screen_brightness_mode {originalMode}", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Best-effort restore; don't mask the original exception
            }
        }
    }

    /// <summary>Executes a shell command on the device and returns the output.</summary>
    public async Task<string> ExecuteShellAsync(
        DeviceData device,
        string command,
        CancellationToken ct = default)
    {
        try
        {
            var receiver = new ConsoleOutputReceiver();
            await _client.ExecuteRemoteCommandAsync(command, device, receiver, ct)
                .ConfigureAwait(false);
            return receiver.ToString().Trim();
        }
        catch (Exception ex) when (ex is not AdbException and not OperationCanceledException)
        {
            throw new AdbException($"Shell command failed: {command}", ex);
        }
    }

    /// <summary>
    /// Discovers active DevTools sockets on the device by inspecting <c>/proc/net/unix</c>.
    /// </summary>
    public async Task<IReadOnlyList<string>> DiscoverDevToolsSocketsAsync(
        DeviceData device,
        CancellationToken ct = default)
    {
        var output = await ExecuteShellAsync(device, "cat /proc/net/unix", ct)
            .ConfigureAwait(false);

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Contains("devtools_remote"))
            .Select(line =>
            {
                // The socket name is the last field in the /proc/net/unix output
                var lastSpace = line.LastIndexOf(' ');
                if (lastSpace < 0) return null;
                var path = line[(lastSpace + 1)..];
                // Strip the leading '@' for abstract sockets
                return path.StartsWith('@') ? path[1..] : path;
            })
            .Where(s => s is not null)
            .Cast<string>()
            .Distinct()
            .ToList();
    }

    /// <summary>Checks whether the specified browser package is installed on the device.</summary>
    public async Task<bool> IsBrowserInstalledAsync(
        DeviceData device,
        ChromeBrowser browser,
        CancellationToken ct = default)
    {
        var output = await ExecuteShellAsync(device, "pm list packages", ct)
            .ConfigureAwait(false);
        return output.Contains($"package:{browser.PackageName}");
    }

    /// <summary>
    /// Full pipeline: launches a browser, sets up CDP forwarding, and connects Playwright.
    /// </summary>
    /// <param name="device">Target device.</param>
    /// <param name="browser">Browser to launch. Defaults to <see cref="ChromeBrowser.Chrome"/>.</param>
    /// <param name="url">Optional URL to navigate to on launch.</param>
    /// <param name="options">Optional Playwright connection options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A connected <see cref="BrowserSession"/>.</returns>
    public async Task<BrowserSession> StartSessionAsync(
        DeviceData device,
        ChromeBrowser? browser = null,
        Uri? url = null,
        BrowserSessionOptions? options = null,
        CancellationToken ct = default)
    {
        await LaunchBrowserAsync(device, browser, url, ct).ConfigureAwait(false);

        var forward = await ForwardCdpAsync(device, browser, ct: ct).ConfigureAwait(false);

        try
        {
            return await BrowserSession.ConnectAsync(forward, options, ct).ConfigureAwait(false);
        }
        catch
        {
            _forwards.Remove(forward);
            await forward.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var forward in _forwards)
        {
            await forward.DisposeAsync().ConfigureAwait(false);
        }

        _forwards.Clear();
    }
}
