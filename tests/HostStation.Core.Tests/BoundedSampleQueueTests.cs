using HostStation.Core.Abstractions;
using HostStation.Core.Buffering;

namespace HostStation.Core.Tests;

public class BoundedSampleQueueTests
{
    [Fact]
    public async Task DropOldest_discards_head_when_full()
    {
        var q = new BoundedSampleQueue(2, OverflowPolicy.DropOldest);
        await q.EnqueueAsync(new TagSample("a", 1, DateTimeOffset.UtcNow));
        await q.EnqueueAsync(new TagSample("b", 2, DateTimeOffset.UtcNow));
        await q.EnqueueAsync(new TagSample("c", 3, DateTimeOffset.UtcNow));

        Assert.True(q.TryRead(out var first));
        Assert.Equal("b", first.Tag);
        Assert.True(q.TryRead(out var second));
        Assert.Equal("c", second.Tag);
    }

    [Fact]
    public async Task DropNewest_increments_dropped_counter()
    {
        var q = new BoundedSampleQueue(1, OverflowPolicy.DropNewest);
        await q.EnqueueAsync(new TagSample("a", 1, DateTimeOffset.UtcNow));
        await q.EnqueueAsync(new TagSample("b", 2, DateTimeOffset.UtcNow));
        Assert.Equal(1, q.Dropped);
        Assert.True(q.TryRead(out var kept));
        Assert.Equal("a", kept.Tag);
    }
}
