namespace Chromedroid;

/// <summary>
/// Represents a single target (tab/page) from the CDP <c>/json</c> endpoint.
/// </summary>
public sealed record CdpTarget(string Id, string Title, string Url, string Type,
    string? FaviconUrl = null);
