using System.Threading.Channels;
using HostStation.Core.Abstractions;

namespace HostStation.Core.Buffering;

public enum OverflowPolicy
{
    DropOldest,
    DropNewest,
    Block,
}

public sealed class BoundedSampleQueue
{
    private readonly Channel<TagSample> _channel;
    private readonly OverflowPolicy _policy;
    private long _dropped;

    public BoundedSampleQueue(int capacity, OverflowPolicy policy = OverflowPolicy.DropOldest)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _policy = policy;
        // DropNewest/Block use Wait so TryWrite fails when full; DropOldest uses channel policy.
        var fullMode = policy == OverflowPolicy.DropOldest
            ? BoundedChannelFullMode.DropOldest
            : BoundedChannelFullMode.Wait;
        _channel = Channel.CreateBounded<TagSample>(new BoundedChannelOptions(capacity)
        {
            FullMode = fullMode,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    public long Dropped => Interlocked.Read(ref _dropped);

    public ValueTask EnqueueAsync(TagSample sample, CancellationToken cancellationToken = default)
    {
        if (_policy == OverflowPolicy.DropNewest)
        {
            if (!_channel.Writer.TryWrite(sample))
                Interlocked.Increment(ref _dropped);
            return ValueTask.CompletedTask;
        }

        if (_channel.Writer.TryWrite(sample))
            return ValueTask.CompletedTask;

        return _channel.Writer.WriteAsync(sample, cancellationToken);
    }

    public IAsyncEnumerable<TagSample> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    public bool TryRead(out TagSample sample) => _channel.Reader.TryRead(out sample!);

    public void Complete() => _channel.Writer.TryComplete();
}
