namespace Chromedroid;

/// <summary>
/// Defines a known Chromium-based browser that can be launched on an Android device.
/// </summary>
public sealed record ChromeBrowser(
    string PackageName,
    string MainActivity,
    string DevToolsSocketName,
    string DisplayName)
{
    public static ChromeBrowser Chrome { get; } = new(
        "com.android.chrome",
        "com.google.android.apps.chrome.Main",
        "chrome_devtools_remote",
        "Chrome");

    public static ChromeBrowser ChromeBeta { get; } = new(
        "com.chrome.beta",
        "com.google.android.apps.chrome.Main",
        "chrome_devtools_remote",
        "Chrome Beta");

    public static ChromeBrowser ChromeDev { get; } = new(
        "com.chrome.dev",
        "com.google.android.apps.chrome.Main",
        "chrome_devtools_remote",
        "Chrome Dev");

    public static ChromeBrowser ChromeCanary { get; } = new(
        "com.chrome.canary",
        "com.google.android.apps.chrome.Main",
        "chrome_devtools_remote",
        "Chrome Canary");

    public static ChromeBrowser Chromium { get; } = new(
        "org.chromium.chrome",
        "com.google.android.apps.chrome.Main",
        "chrome_devtools_remote",
        "Chromium");

    public static ChromeBrowser Brave { get; } = new(
        "com.brave.browser",
        "com.brave.browser.ChromeTabbedActivity",
        "chrome_devtools_remote",
        "Brave");

    public static ChromeBrowser BraveBeta { get; } = new(
        "com.brave.browser_beta",
        "com.brave.browser.ChromeTabbedActivity",
        "chrome_devtools_remote",
        "Brave Beta");

    public static ChromeBrowser BraveNightly { get; } = new(
        "com.brave.browser_nightly",
        "com.brave.browser.ChromeTabbedActivity",
        "chrome_devtools_remote",
        "Brave Nightly");

    public static ChromeBrowser Vivaldi { get; } = new(
        "com.vivaldi.browser",
        "com.vivaldi.browser.ChromeTabbedActivity",
        "chrome_devtools_remote",
        "Vivaldi");
}
