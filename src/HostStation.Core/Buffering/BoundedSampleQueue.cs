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
    private readonly int _capacity;
    private long _dropped;
    private long _enqueued;

    public BoundedSampleQueue(int capacity, OverflowPolicy policy = OverflowPolicy.DropOldest)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _policy = policy;
        // DropOldest is handled manually so Dropped is observable for backpressure metrics.
        var fullMode = policy == OverflowPolicy.Block
            ? BoundedChannelFullMode.Wait
            : BoundedChannelFullMode.Wait;
        _channel = Channel.CreateBounded<TagSample>(new BoundedChannelOptions(capacity)
        {
            FullMode = fullMode,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    public int Capacity => _capacity;
    public OverflowPolicy Policy => _policy;
    public long Dropped => Interlocked.Read(ref _dropped);
    public long Enqueued => Interlocked.Read(ref _enqueued);
    public int Count => _channel.Reader.Count;

    public ValueTask EnqueueAsync(TagSample sample, CancellationToken cancellationToken = default)
    {
        return _policy switch
        {
            OverflowPolicy.DropNewest => EnqueueDropNewest(sample),
            OverflowPolicy.DropOldest => EnqueueDropOldest(sample),
            _ => EnqueueBlockAsync(sample, cancellationToken),
        };
    }

    private ValueTask EnqueueDropNewest(TagSample sample)
    {
        if (_channel.Writer.TryWrite(sample))
        {
            Interlocked.Increment(ref _enqueued);
            return ValueTask.CompletedTask;
        }

        Interlocked.Increment(ref _dropped);
        return ValueTask.CompletedTask;
    }

    private ValueTask EnqueueDropOldest(TagSample sample)
    {
        while (!_channel.Writer.TryWrite(sample))
        {
            if (_channel.Reader.TryRead(out _))
                Interlocked.Increment(ref _dropped);
            else
                Thread.SpinWait(4);
        }

        Interlocked.Increment(ref _enqueued);
        return ValueTask.CompletedTask;
    }

    private async ValueTask EnqueueBlockAsync(TagSample sample, CancellationToken cancellationToken)
    {
        if (_channel.Writer.TryWrite(sample))
        {
            Interlocked.Increment(ref _enqueued);
            return;
        }

        await _channel.Writer.WriteAsync(sample, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _enqueued);
    }

    public IAsyncEnumerable<TagSample> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    public bool TryRead(out TagSample sample) => _channel.Reader.TryRead(out sample!);

    public void Complete() => _channel.Writer.TryComplete();
}
