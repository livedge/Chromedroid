using AdvancedSharpAdbClient.Models;
using Microsoft.Playwright;

namespace Chromedroid;

/// <summary>
/// High-level facade that binds to a single Android device and manages a persistent
/// Playwright browser session. Handles session lifecycle, reconnection, and common
/// device interactions (wake/unlock).
/// </summary>
public sealed class AndroidDriver : IAsyncDisposable
{
    private readonly AdbBridge _bridge;
    private readonly DeviceData _device;
    private readonly ChromeBrowser _browser;
    private readonly BrowserSessionOptions? _options;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    private BrowserSession? _session;
    private CdpForward? _forward;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="AndroidDriver"/> bound to a specific device.
    /// No I/O is performed until a method is called.
    /// </summary>
    /// <param name="bridge">The ADB bridge to use for device communication.</param>
    /// <param name="device">The target device.</param>
    /// <param name="browser">Browser to automate. Defaults to <see cref="ChromeBrowser.Chrome"/>.</param>
    /// <param name="options">Optional Playwright connection options.</param>
    public AndroidDriver(
        AdbBridge bridge,
        DeviceData device,
        ChromeBrowser? browser = null,
        BrowserSessionOptions? options = null)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _device = device;
        _browser = browser ?? ChromeBrowser.Chrome;
        _options = options;
    }

    /// <summary>The ADB bridge this driver uses.</summary>
    public AdbBridge Bridge => _bridge;

    /// <summary>The device this driver is bound to.</summary>
    public DeviceData Device => _device;

    /// <summary>The browser this driver automates.</summary>
    public ChromeBrowser Browser => _browser;

    /// <summary>The current browser session, if connected.</summary>
    public BrowserSession? Session => _session;

    /// <summary>
    /// Returns a page navigated to the given URL. Reuses the existing session when
    /// healthy; creates a new session (launching the browser and forwarding CDP) when
    /// no session exists or the previous one has disconnected.
    /// </summary>
    public async Task<IPage> GetPageAsync(string url, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Tear down stale session
            if (_session is not null && !_session.Browser.IsConnected)
                await TearDownSessionAsync().ConfigureAwait(false);

            // Start new session if needed
            if (_session is null)
            {
                await StartNewSessionAsync(url, ct).ConfigureAwait(false);

                // The browser was launched with this URL — return the existing page
                if (_session!.Pages.Count > 0)
                    return _session.Pages[^1];

                // Fallback: create a page and navigate
                var page = await _session.NewPageAsync(ct).ConfigureAwait(false);
                await page.GotoAsync(url).ConfigureAwait(false);
                return page;
            }

            // Healthy session — open a new page
            var newPage = await _session.NewPageAsync(ct).ConfigureAwait(false);
            await newPage.GotoAsync(url).ConfigureAwait(false);
            return newPage;
        }
        catch (Exception ex) when (ex is not AdbException and not OperationCanceledException)
        {
            throw new AdbException("Failed to get page.", ex);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <summary>
    /// Ensures the device screen is on and unlocked. Optionally enters a PIN to dismiss
    /// the lock screen.
    /// </summary>
    public async Task EnsureDeviceAwakeAsync(string? pin = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Check if screen is on
            var powerState = await _bridge.ExecuteShellAsync(_device, "dumpsys power", ct)
                .ConfigureAwait(false);

            var isAwake = powerState.Contains("mWakefulness=Awake", StringComparison.OrdinalIgnoreCase)
                          || powerState.Contains("Display Power: state=ON", StringComparison.OrdinalIgnoreCase);

            if (!isAwake)
            {
                await _bridge.ExecuteShellAsync(_device, "input keyevent 224", ct)
                    .ConfigureAwait(false);
                await Task.Delay(500, ct).ConfigureAwait(false);
            }

            // Check if keyguard (lock screen) is showing
            var windowState = await _bridge.ExecuteShellAsync(_device, "dumpsys window", ct)
                .ConfigureAwait(false);

            var isLocked = windowState.Contains("mDreamingLockscreen=true", StringComparison.OrdinalIgnoreCase)
                           || windowState.Contains("isStatusBarKeyguard=true", StringComparison.OrdinalIgnoreCase)
                           || windowState.Contains("showing=true", StringComparison.OrdinalIgnoreCase);

            if (!isLocked)
                return;

            // Swipe to dismiss lock screen
            await _bridge.ExecuteShellAsync(_device, "input swipe 540 1800 540 800 300", ct)
                .ConfigureAwait(false);
            await Task.Delay(500, ct).ConfigureAwait(false);

            // Enter PIN if provided
            if (pin is not null)
            {
                await _bridge.ExecuteShellAsync(_device, $"input text {pin}", ct)
                    .ConfigureAwait(false);
                await _bridge.ExecuteShellAsync(_device, "input keyevent 66", ct)
                    .ConfigureAwait(false);
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not AdbException and not OperationCanceledException)
        {
            throw new AdbException("Failed to wake/unlock device.", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _sessionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await TearDownSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _sessionLock.Release();
            _sessionLock.Dispose();
        }
    }

    private async Task StartNewSessionAsync(string url, CancellationToken ct)
    {
        await _bridge.LaunchBrowserAsync(_device, _browser, new Uri(url), ct)
            .ConfigureAwait(false);

        _forward = await _bridge.ForwardCdpAsync(_device, _browser, ct: ct)
            .ConfigureAwait(false);

        _session = await BrowserSession.ConnectAsync(_forward, _options, ct)
            .ConfigureAwait(false);
    }

    private async Task TearDownSessionAsync()
    {
        if (_session is not null)
        {
            try { await _session.DisposeAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }
            _session = null;
        }

        if (_forward is not null)
        {
            try { await _forward.DisposeAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }
            _forward = null;
        }
    }
}
