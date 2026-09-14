using HostStation.Core.Abstractions;
using HostStation.Core.Reconnect;

namespace HostStation.Core.Tests;

public class ReconnectPolicyTests
{
    private sealed class FlakyTransport : IDeviceTransport
    {
        private int _attempts;

        public FlakyTransport(int succeedOnAttempt) => SucceedOnAttempt = succeedOnAttempt;
        public int SucceedOnAttempt { get; }
        public string DeviceId => "flaky";
        public bool IsConnected { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            _attempts++;
            if (_attempts < SucceedOnAttempt)
                throw new IOException("down");
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFramesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void Delays_grow_until_max()
    {
        var policy = new ReconnectPolicy
        {
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(400),
            Multiplier = 2,
            MaxAttempts = 4,
        };
        var delays = policy.Delays().Select(d => d.TotalMilliseconds).ToList();
        Assert.Equal(new[] { 100d, 200d, 400d, 400d }, delays);
    }

    [Fact]
    public async Task ReconnectLoop_succeeds_after_failures()
    {
        var transport = new FlakyTransport(succeedOnAttempt: 3);
        var policy = new ReconnectPolicy
        {
            InitialDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(5),
            MaxAttempts = 5,
        };
        await ReconnectLoop.RunAsync(transport, policy);
        Assert.True(transport.IsConnected);
    }
}
