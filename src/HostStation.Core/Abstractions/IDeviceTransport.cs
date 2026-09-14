namespace HostStation.Core.Abstractions;

public interface IDeviceTransport : IAsyncDisposable
{
    string DeviceId { get; }
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFramesAsync(CancellationToken cancellationToken = default);
}
