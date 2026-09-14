using HostStation.Protocols.Framing;

namespace HostStation.Core.Tests;

public class ModbusFramingTests
{
    [Fact]
    public void Rtu_crc_roundtrip()
    {
        // classic example: 01 03 00 00 00 0A → CRC 0xCDC5
        ReadOnlySpan<byte> pdu = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A];
        var frame = ModbusRtuCrc.Append(pdu);
        Assert.Equal(8, frame.Length);
        Assert.True(ModbusRtuCrc.Validate(frame));
        Assert.Equal(0xC5, frame[^2]);
        Assert.Equal(0xCD, frame[^1]);
    }

    [Fact]
    public void Tcp_read_holding_frame_shape()
    {
        var frame = ModbusTcpFrame.BuildReadHoldingRegisters(1, 1, 0, 10);
        Assert.Equal(12, frame.Length);
        Assert.Equal(0x03, frame[7]);
        Assert.Equal(10, frame[10] << 8 | frame[11]);
    }
}
