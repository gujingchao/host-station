namespace HostStation.Core.Reconnect;

public sealed class ReconnectPolicy
{
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(10);
    public double Multiplier { get; init; } = 2.0;
    public int? MaxAttempts { get; init; }

    public IEnumerable<TimeSpan> Delays()
    {
        var delay = InitialDelay;
        var attempt = 0;
        while (MaxAttempts is null || attempt < MaxAttempts)
        {
            yield return delay;
            attempt++;
            var nextMs = Math.Min(MaxDelay.TotalMilliseconds, delay.TotalMilliseconds * Multiplier);
            delay = TimeSpan.FromMilliseconds(nextMs);
        }
    }
}
