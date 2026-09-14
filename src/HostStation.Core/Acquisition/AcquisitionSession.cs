using HostStation.Core.Abstractions;
using HostStation.Core.Buffering;
using HostStation.Core.Reconnect;

namespace HostStation.Core.Acquisition;

/// <summary>
/// Polls one protocol adapter into a shared bounded queue.
/// Transport reconnect is optional when a live transport is supplied.
/// </summary>
public sealed class AcquisitionSession : IAsyncDisposable
{
    private readonly IProtocolAdapter _adapter;
    private readonly BoundedSampleQueue _queue;
    private readonly TimeSpan _interval;
    private readonly IDeviceTransport? _transport;
    private readonly ReconnectPolicy _reconnect;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public AcquisitionSession(
        IProtocolAdapter adapter,
        BoundedSampleQueue queue,
        TimeSpan? pollInterval = null,
        IDeviceTransport? transport = null,
        ReconnectPolicy? reconnectPolicy = null)
    {
        _adapter = adapter;
        _queue = queue;
        _interval = pollInterval ?? TimeSpan.FromMilliseconds(200);
        _transport = transport;
        _reconnect = reconnectPolicy ?? new ReconnectPolicy { MaxAttempts = 5 };
    }

    public string Name => _adapter.Name;
    public long PollCount { get; private set; }
    public long ErrorCount { get; private set; }
    public bool IsRunning => _loop is { IsCompleted: false };

    public void Start()
    {
        if (_loop is { IsCompleted: false })
            return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_transport is not null && !_transport.IsConnected)
                    await ReconnectLoop.RunAsync(_transport, _reconnect, cancellationToken).ConfigureAwait(false);

                var samples = await _adapter.PollAsync(cancellationToken).ConfigureAwait(false);
                foreach (var sample in samples)
                    await _queue.EnqueueAsync(sample, cancellationToken).ConfigureAwait(false);
                PollCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                ErrorCount++;
                if (_transport is { IsConnected: true })
                {
                    try { await _transport.DisconnectAsync(cancellationToken).ConfigureAwait(false); }
                    catch { /* ignore */ }
                }
            }

            await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
