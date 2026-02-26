using System.Collections.ObjectModel;
using AdvancedSharpAdbClient.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chromedroid.Gui;

public sealed partial class BrowserStatusItem : ObservableObject, IAsyncDisposable
{
    private readonly AdbBridge _bridge;
    private readonly ChromeBrowser _browser;
    private CdpForward? _forward;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string? _pagesError;

    public string DisplayName => _browser.DisplayName;

    public string PackageName => _browser.PackageName;

    public ObservableCollection<CdpTargetViewModel> OpenPages { get; } = [];

    public BrowserStatusItem(AdbBridge bridge, ChromeBrowser browser)
    {
        _bridge = bridge;
        _browser = browser;
    }

    public async Task RefreshPagesAsync(DeviceData device, CancellationToken ct = default)
    {
        try
        {
            _forward ??= await _bridge.ForwardCdpAsync(device, _browser, ct: ct).ConfigureAwait(false);

            var targets = await AdbBridge.ListTargetsAsync(_forward, ct).ConfigureAwait(false);

            var pages = targets.Where(t => t.Type == "page").ToList();

            // Reconcile: remove stale
            for (var i = OpenPages.Count - 1; i >= 0; i--)
            {
                if (!pages.Any(p => p.Id == OpenPages[i].Id))
                    OpenPages.RemoveAt(i);
            }

            // Update existing or add new
            foreach (var page in pages)
            {
                var existing = OpenPages.FirstOrDefault(p => p.Id == page.Id);
                if (existing is not null)
                {
                    existing.Update(page);
                }
                else
                {
                    OpenPages.Add(new CdpTargetViewModel(page));
                }
            }

            PagesError = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            PagesError = ex.Message;

            // Tear down stale forward so next tick retries
            if (_forward is not null)
            {
                await _forward.DisposeAsync().ConfigureAwait(false);
                _forward = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        OpenPages.Clear();
        PagesError = null;

        if (_forward is not null)
        {
            await _forward.DisposeAsync().ConfigureAwait(false);
            _forward = null;
        }
    }
}
