using System.Runtime.CompilerServices;
using HostStation.Core.Abstractions;

namespace HostStation.Protocols.Tcp;

/// <summary>TCP transport stub — real sockets plug in behind the same interface.</summary>
public sealed class TcpTransport : IDeviceTransport
{
    private readonly Queue<byte[]> _inbox = new();
    private bool _connected;

    public TcpTransport(string deviceId, string host, int port)
    {
        DeviceId = deviceId;
        Host = host;
        Port = port;
    }

    public string DeviceId { get; }
    public string Host { get; }
    public int Port { get; }
    public bool IsConnected => _connected;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = false;
        return Task.CompletedTask;
    }

    public Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (!_connected) throw new InvalidOperationException($"{DeviceId} is not connected.");
        _inbox.Enqueue(payload.ToArray());
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_inbox.Count > 0)
            {
                yield return _inbox.Dequeue();
                continue;
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }
}
