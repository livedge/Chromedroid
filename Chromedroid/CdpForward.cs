using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;

namespace Chromedroid;

/// <summary>
/// Represents an active CDP port forward from a local TCP port to a DevTools Unix socket
/// on an Android device. Disposing removes the forward.
/// </summary>
public sealed class CdpForward : IAsyncDisposable
{
    private readonly IAdbClient _client;
    private readonly DeviceData _device;
    private bool _disposed;

    internal CdpForward(IAdbClient client, DeviceData device, int localPort, string socketName)
    {
        _client = client;
        _device = device;
        LocalPort = localPort;
        SocketName = socketName;
    }

    public int LocalPort { get; }

    public string SocketName { get; }

    public Uri EndpointUri => new($"http://127.0.0.1:{LocalPort}");

    public Uri WebSocketUri => new($"ws://127.0.0.1:{LocalPort}");

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            await _client.RemoveForwardAsync(_device, LocalPort, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort: the device may have disconnected already.
        }
    }
}
