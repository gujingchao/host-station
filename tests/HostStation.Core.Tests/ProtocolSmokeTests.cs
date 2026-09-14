using HostStation.Protocols.Modbus;
using HostStation.Protocols.Serial;
using HostStation.Protocols.Tcp;

namespace HostStation.Core.Tests;

public class ProtocolSmokeTests
{
    [Fact]
    public async Task Serial_echoes_written_frame()
    {
        await using var t = new SerialTransport("dev-1", "COM1");
        await t.ConnectAsync();
        await t.WriteAsync(new byte[] { 1, 2, 3 });
        await foreach (var frame in t.ReadFramesAsync(CancellationToken.None))
        {
            Assert.Equal(new byte[] { 1, 2, 3 }, frame.ToArray());
            break;
        }
    }

    [Fact]
    public async Task Tcp_echoes_written_frame()
    {
        await using var t = new TcpTransport("plc-1", "127.0.0.1", 502);
        await t.ConnectAsync();
        await t.WriteAsync(new byte[] { 9 });
        await foreach (var frame in t.ReadFramesAsync(CancellationToken.None))
        {
            Assert.Equal(new byte[] { 9 }, frame.ToArray());
            break;
        }
    }

    [Fact]
    public async Task Modbus_poll_returns_tags()
    {
        var adapter = new ModbusAdapter("line-a", ModbusMode.Tcp);
        var samples = await adapter.PollAsync();
        Assert.Contains(samples, s => s.Tag == "HR40001");
    }
}
