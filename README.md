# Chromedroid

[![build](https://github.com/livedge/Chromedroid/actions/workflows/ci.yml/badge.svg)](https://github.com/livedge/Chromedroid/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/badge/NuGet-GitHub%20Packages-blue)](https://github.com/livedge/Chromedroid/pkgs/nuget/Chromedroid)

.NET library for orchestrating Chromium-based browsers on Android devices via ADB and the Chrome DevTools Protocol (CDP). Automate Chrome, Brave, Vivaldi, and other Chromium browsers running on physical Android devices using Playwright.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [ADB (Android Debug Bridge)](https://developer.android.com/tools/adb) installed and on your PATH, or provide the path explicitly
- One or more Android devices connected via USB with USB debugging enabled

## Installation

Install from GitHub Packages:

```bash
dotnet add package Chromedroid --source https://nuget.pkg.github.com/livedge/index.json
```

## Quick Start

```csharp
await using var bridge = AdbBridge.Create();
var devices = await bridge.GetOnlineDevicesAsync();
var device = devices[0];

// Launch Chrome and get a Playwright page in one call
await using var session = await bridge.StartSessionAsync(device);
var page = await session.NewPageAsync();
await page.GotoAsync("https://example.com");

Console.WriteLine(await page.TitleAsync());
```

## Usage

### Discovering Devices

```csharp
await using var bridge = AdbBridge.Create();

// All connected devices (online, offline, unauthorized, etc.)
var all = await bridge.GetDevicesAsync();

// Only online (ready) devices
var online = await bridge.GetOnlineDevicesAsync();
```

### Launching a Browser

```csharp
// Launch default browser (Chrome)
await bridge.LaunchBrowserAsync(device);

// Launch a specific browser with a URL
await bridge.LaunchBrowserAsync(device, ChromeBrowser.Brave, new Uri("https://example.com"));
```

### Browser Automation with Playwright

`StartSessionAsync` is the full pipeline — it launches the browser, sets up CDP port forwarding, and connects Playwright:

```csharp
await using var session = await bridge.StartSessionAsync(
    device,
    ChromeBrowser.Chrome,
    new Uri("https://example.com"),
    new BrowserSessionOptions { SlowMo = 50 });

var page = await session.NewPageAsync();
await page.GotoAsync("https://example.com/login");
await page.FillAsync("#username", "user");
await page.ClickAsync("button[type=submit]");
```

### Using AndroidDriver

`AndroidDriver` is a high-level facade that binds to a single device and manages session lifecycle automatically, reconnecting when needed:

```csharp
await using var bridge = AdbBridge.Create();
var device = (await bridge.GetOnlineDevicesAsync())[0];

await using var driver = new AndroidDriver(bridge, device);

// Wake the device and unlock with PIN
await driver.EnsureDeviceAwakeAsync(pin: "1234");

// Get a page — creates or reuses a session automatically
var page = await driver.GetPageAsync("https://example.com");
Console.WriteLine(await page.TitleAsync());
```

### CDP Port Forwarding (Manual)

For lower-level control, set up CDP forwarding and connect manually:

```csharp
await using var forward = await bridge.ForwardCdpAsync(device, ChromeBrowser.Chrome);

// forward.EndpointUri  → http://127.0.0.1:{port}
// forward.WebSocketUri → ws://127.0.0.1:{port}

// List open tabs
var targets = await AdbBridge.ListTargetsAsync(forward);
foreach (var target in targets)
    Console.WriteLine($"{target.Title} — {target.Url}");

// Connect Playwright to the forwarded port
await using var session = await BrowserSession.ConnectAsync(forward);
```

### Device Identification

Flash a device's screen to maximum brightness to visually identify it:

```csharp
await bridge.FlashScreenAsync(device, durationMs: 2000);
```

### Shell Commands

Execute arbitrary ADB shell commands:

```csharp
string output = await bridge.ExecuteShellAsync(device, "getprop ro.product.model");
```

## Supported Browsers

| Browser | Identifier |
|---|---|
| Chrome | `ChromeBrowser.Chrome` |
| Chrome Beta | `ChromeBrowser.ChromeBeta` |
| Chrome Dev | `ChromeBrowser.ChromeDev` |
| Chrome Canary | `ChromeBrowser.ChromeCanary` |
| Chromium | `ChromeBrowser.Chromium` |
| Brave | `ChromeBrowser.Brave` |
| Brave Beta | `ChromeBrowser.BraveBeta` |
| Brave Nightly | `ChromeBrowser.BraveNightly` |
| Vivaldi | `ChromeBrowser.Vivaldi` |

## License

[MIT](LICENSE)
