namespace HostStation.Protocols.Framing;

/// <summary>Modbus RTU CRC16 (poly 0xA001).</summary>
public static class ModbusRtuCrc
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                var lsb = (crc & 1) != 0;
                crc >>= 1;
                if (lsb) crc ^= 0xA001;
            }
        }
        return crc;
    }

    public static byte[] Append(ReadOnlySpan<byte> pdu)
    {
        var crc = Compute(pdu);
        var frame = new byte[pdu.Length + 2];
        pdu.CopyTo(frame);
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    public static bool Validate(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3) return false;
        var expected = Compute(frame[..^2]);
        var actual = (ushort)(frame[^2] | (frame[^1] << 8));
        return expected == actual;
    }
}
