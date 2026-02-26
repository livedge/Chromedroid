using Microsoft.Playwright;

namespace Chromedroid;

/// <summary>
/// A Playwright-connected browser session. Owns the <see cref="IPlaywright"/> and
/// <see cref="IBrowser"/> instances but does not own the underlying <see cref="CdpForward"/>.
/// </summary>
public sealed class BrowserSession : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private bool _disposed;

    private BrowserSession(IPlaywright playwright, IBrowser browser, CdpForward forward)
    {
        _playwright = playwright;
        Browser = browser;
        Forward = forward;
    }

    /// <summary>The connected Playwright browser.</summary>
    public IBrowser Browser { get; }

    /// <summary>The CDP forward this session is connected through.</summary>
    public CdpForward Forward { get; }

    /// <summary>The default browser context provided by the CDP connection.</summary>
    public IBrowserContext Context => Browser.Contexts[0];

    /// <summary>Pages in the default context.</summary>
    public IReadOnlyList<IPage> Pages => Context.Pages;

    /// <summary>
    /// Connects to a browser's DevTools socket via Playwright.
    /// </summary>
    /// <param name="forward">An active CDP port forward.</param>
    /// <param name="options">Optional connection options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A connected <see cref="BrowserSession"/>.</returns>
    public static async Task<BrowserSession> ConnectAsync(
        CdpForward forward,
        BrowserSessionOptions? options = null,
        CancellationToken ct = default)
    {
        IPlaywright? playwright = null;
        try
        {
            playwright = await Playwright.CreateAsync().ConfigureAwait(false);

            var connectOptions = new BrowserTypeConnectOverCDPOptions();
            if (options?.Timeout is { } timeout)
                connectOptions.Timeout = timeout;
            if (options?.SlowMo is { } slowMo)
                connectOptions.SlowMo = slowMo;
            if (options?.Headers is { } headers)
                connectOptions.Headers = headers;

            var browser = await playwright.Chromium
                .ConnectOverCDPAsync(forward.EndpointUri.ToString(), connectOptions)
                .ConfigureAwait(false);

            return new BrowserSession(playwright, browser, forward);
        }
        catch
        {
            playwright?.Dispose();
            throw;
        }
    }

    /// <summary>Creates a new page in the default browser context.</summary>
    public async Task<IPage> NewPageAsync(CancellationToken ct = default)
    {
        return await Context.NewPageAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            await Browser.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort: the browser or device may have disconnected.
        }

        _playwright.Dispose();
    }
}
