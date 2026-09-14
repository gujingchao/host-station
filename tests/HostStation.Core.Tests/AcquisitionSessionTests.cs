using HostStation.Core.Abstractions;
using HostStation.Core.Acquisition;
using HostStation.Core.Buffering;
using HostStation.Protocols.Modbus;

namespace HostStation.Core.Tests;

public class AcquisitionSessionTests
{
    [Fact]
    public async Task Session_polls_into_queue()
    {
        var queue = new BoundedSampleQueue(32);
        await using var session = new AcquisitionSession(
            new ModbusAdapter("line-a", ModbusMode.Tcp),
            queue,
            pollInterval: TimeSpan.FromMilliseconds(30));
        session.Start();
        await Task.Delay(120);
        await session.StopAsync();

        Assert.True(session.PollCount >= 1);
        Assert.True(queue.TryRead(out var sample));
        Assert.Equal("HR40001", sample.Tag);
    }

    [Fact]
    public async Task Hub_runs_multiple_adapters()
    {
        var queue = new BoundedSampleQueue(64, OverflowPolicy.DropOldest);
        await using var hub = new AcquisitionHub(queue);
        hub.Add(new ModbusAdapter("a", ModbusMode.Tcp), TimeSpan.FromMilliseconds(40));
        hub.Add(new ModbusAdapter("b", ModbusMode.Rtu, () => new Dictionary<string, double> { ["IR30001"] = 9 }),
            TimeSpan.FromMilliseconds(40));
        hub.StartAll();
        await Task.Delay(150);
        await hub.StopAllAsync();

        var tags = new HashSet<string>();
        while (queue.TryRead(out var s))
            tags.Add(s.Tag);
        Assert.Contains("HR40001", tags);
        Assert.Contains("IR30001", tags);
    }
}
