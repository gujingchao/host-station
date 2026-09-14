using System.Diagnostics;
using HostStation.Core.Abstractions;
using HostStation.Core.Buffering;

namespace HostStation.Core.Tests;

public class BackpressureBenchTests
{
    [Fact]
    public async Task DropOldest_sheds_under_burst_and_keeps_newest()
    {
        var profile = BackpressureProfile.SoakDropNewest with
        {
            QueueCapacity = 8,
            Overflow = OverflowPolicy.DropOldest,
        };
        var q = profile.CreateQueue();
        for (var i = 0; i < 40; i++)
            await q.EnqueueAsync(new TagSample("t", i, DateTimeOffset.UtcNow));

        Assert.True(q.Dropped >= 30);
        Assert.True(q.TryRead(out var first));
        Assert.True(first.Value >= 32);
    }

    [Fact]
    public async Task DropNewest_rejects_overflow_without_evicting_head()
    {
        var q = BackpressureProfile.SoakDropNewest.CreateQueue();
        await q.EnqueueAsync(new TagSample("head", 1, DateTimeOffset.UtcNow));
        for (var i = 0; i < q.Capacity + 10; i++)
            await q.EnqueueAsync(new TagSample("x", i, DateTimeOffset.UtcNow));

        Assert.True(q.Dropped >= 10);
        Assert.True(q.TryRead(out var head));
        Assert.Equal("head", head.Tag);
    }

    [Fact]
    public async Task Block_applies_backpressure_without_drops()
    {
        var q = new BoundedSampleQueue(4, OverflowPolicy.Block);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var producer = Task.Run(async () =>
        {
            for (var i = 0; i < 20; i++)
                await q.EnqueueAsync(new TagSample("b", i, DateTimeOffset.UtcNow), cts.Token);
        }, cts.Token);

        var consumed = 0;
        while (consumed < 20)
        {
            if (q.TryRead(out _))
                consumed++;
            else
                await Task.Delay(5, cts.Token);
        }

        await producer;
        Assert.Equal(0, q.Dropped);
        Assert.Equal(20, q.Enqueued);
    }

    [Fact]
    public async Task Device_rate_limiter_protects_shared_queue()
    {
        var q = new BoundedSampleQueue(256, OverflowPolicy.DropOldest);
        var hot = new DeviceRateLimiter(maxSamplesPerSecond: 50);
        var cold = new DeviceRateLimiter(maxSamplesPerSecond: 200);

        async Task Spray(string tag, DeviceRateLimiter limiter, int n)
        {
            for (var i = 0; i < n; i++)
            {
                if (limiter.TryAcquire())
                    await q.EnqueueAsync(new TagSample(tag, i, DateTimeOffset.UtcNow));
            }
        }

        await Task.WhenAll(Spray("hot", hot, 400), Spray("cold", cold, 100));

        Assert.True(hot.Rejected > 0);
        var tags = new HashSet<string>();
        while (q.TryRead(out var s))
            tags.Add(s.Tag);
        Assert.Contains("cold", tags);
    }

    [Fact]
    public async Task Throughput_floor_under_multi_producer_drop_oldest()
    {
        var q = new BoundedSampleQueue(1024, OverflowPolicy.DropOldest);
        const int producers = 4;
        const int each = 5000;
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, producers).Select(p => Task.Run(async () =>
        {
            for (var i = 0; i < each; i++)
                await q.EnqueueAsync(new TagSample($"d{p}", i, DateTimeOffset.UtcNow));
        })));
        sw.Stop();

        var total = producers * each;
        // Every write eventually lands (DropOldest evicts then accepts) → Enqueued == attempts.
        Assert.Equal(total, q.Enqueued);
        Assert.True(q.Dropped >= total - q.Capacity);
        var sps = total / Math.Max(sw.Elapsed.TotalSeconds, 1e-6);
        // CI floor: keep generous for shared runners; guards catastrophic regressions.
        Assert.True(sps > 20_000, $"throughput {sps:F0} sps too low");
    }
}
