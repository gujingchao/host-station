namespace HostStation.Core.Buffering;

/// <summary>
/// Recommended capacity / overflow / per-device rate for industrial HMI fan-in.
/// him owns throughput &amp; backpressure policy (not transport ownership).
/// </summary>
public sealed record BackpressureProfile(
    int QueueCapacity,
    OverflowPolicy Overflow,
    int MaxSamplesPerSecondPerDevice)
{
    /// <summary>Realtime trends: prefer freshest samples, drop oldest under load.</summary>
    public static BackpressureProfile RealtimeUi { get; } =
        new(2048, OverflowPolicy.DropOldest, 200);

    /// <summary>Alarm / audit path: never silently drop; apply producer backpressure.</summary>
    public static BackpressureProfile ReliableAlarms { get; } =
        new(4096, OverflowPolicy.Block, 50);

    /// <summary>Burst soak: small queue + drop newest to measure shed rate.</summary>
    public static BackpressureProfile SoakDropNewest { get; } =
        new(64, OverflowPolicy.DropNewest, 1000);

    public BoundedSampleQueue CreateQueue() => new(QueueCapacity, Overflow);
}
