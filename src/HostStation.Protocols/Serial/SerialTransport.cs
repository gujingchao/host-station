using System.Runtime.CompilerServices;
using HostStation.Core.Abstractions;

namespace HostStation.Protocols.Serial;

/// <summary>Serial transport stub — real SerialPort wiring lands with device config.</summary>
public sealed class SerialTransport : IDeviceTransport
{
    private readonly Queue<byte[]> _inbox = new();
    private bool _connected;

    public SerialTransport(string deviceId, string portName)
    {
        DeviceId = deviceId;
        PortName = portName;
    }

    public string DeviceId { get; }
    public string PortName { get; }
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
        EnsureConnected();
        // echo for unit tests
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

    private void EnsureConnected()
    {
        if (!_connected) throw new InvalidOperationException($"{DeviceId} is not connected.");
    }
}
