namespace HostStation.Core.Buffering;

/// <summary>Token-bucket style limiter so one hot device cannot starve the shared queue.</summary>
public sealed class DeviceRateLimiter
{
    private readonly double _tokensPerSecond;
    private readonly double _burst;
    private readonly object _gate = new();
    private double _tokens;
    private long _lastTicks;
    private long _rejected;

    public DeviceRateLimiter(int maxSamplesPerSecond, double burstMultiplier = 2.0)
    {
        if (maxSamplesPerSecond < 1) throw new ArgumentOutOfRangeException(nameof(maxSamplesPerSecond));
        _tokensPerSecond = maxSamplesPerSecond;
        _burst = maxSamplesPerSecond * burstMultiplier;
        _tokens = _burst;
        _lastTicks = Environment.TickCount64;
    }

    public long Rejected => Interlocked.Read(ref _rejected);

    public bool TryAcquire(int permits = 1)
    {
        lock (_gate)
        {
            Refill();
            if (_tokens < permits)
            {
                Interlocked.Increment(ref _rejected);
                return false;
            }

            _tokens -= permits;
            return true;
        }
    }

    private void Refill()
    {
        var now = Environment.TickCount64;
        var elapsedMs = now - _lastTicks;
        if (elapsedMs <= 0) return;
        _lastTicks = now;
        _tokens = Math.Min(_burst, _tokens + (_tokensPerSecond * elapsedMs / 1000.0));
    }
}
