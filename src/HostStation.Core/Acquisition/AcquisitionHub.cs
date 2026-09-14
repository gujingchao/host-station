using HostStation.Core.Abstractions;
using HostStation.Core.Buffering;

namespace HostStation.Core.Acquisition;

/// <summary>Multi-device acquisition coordinator feeding one UI-facing sample queue.</summary>
public sealed class AcquisitionHub : IAsyncDisposable
{
    private readonly List<AcquisitionSession> _sessions = new();
    private readonly object _gate = new();

    public AcquisitionHub(BoundedSampleQueue queue)
    {
        Queue = queue;
    }

    public BoundedSampleQueue Queue { get; }

    public IReadOnlyList<AcquisitionSession> Sessions
    {
        get { lock (_gate) return _sessions.ToList(); }
    }

    public AcquisitionSession Add(IProtocolAdapter adapter, TimeSpan? pollInterval = null, IDeviceTransport? transport = null)
    {
        var session = new AcquisitionSession(adapter, Queue, pollInterval, transport);
        lock (_gate) _sessions.Add(session);
        return session;
    }

    public void StartAll()
    {
        foreach (var s in Sessions)
            s.Start();
    }

    public async Task StopAllAsync()
    {
        foreach (var s in Sessions)
            await s.StopAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync().ConfigureAwait(false);
        Queue.Complete();
    }
}
