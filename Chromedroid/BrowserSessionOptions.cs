namespace Chromedroid;

/// <summary>
/// Options for connecting to a browser via the Chrome DevTools Protocol.
/// Insulates consumers from Playwright-specific types.
/// </summary>
public sealed record BrowserSessionOptions
{
    /// <summary>CDP connection timeout in milliseconds.</summary>
    public float? Timeout { get; init; }

    /// <summary>Delay between Playwright operations in milliseconds.</summary>
    public float? SlowMo { get; init; }

    /// <summary>Extra HTTP headers sent with the CDP connection request.</summary>
    public IDictionary<string, string>? Headers { get; init; }
}
