namespace HostStation.Protocols.Framing;

/// <summary>Minimal MBAP + PDU builder for unit tests / adapters.</summary>
public static class ModbusTcpFrame
{
    public static byte[] BuildReadHoldingRegisters(ushort transactionId, byte unitId, ushort startAddress, ushort quantity)
    {
        // MBAP(7) + function(1) + start(2) + qty(2) = 12
        var frame = new byte[12];
        frame[0] = (byte)(transactionId >> 8);
        frame[1] = (byte)(transactionId & 0xFF);
        frame[2] = 0;
        frame[3] = 0;
        frame[4] = 0;
        frame[5] = 6; // length
        frame[6] = unitId;
        frame[7] = 0x03;
        frame[8] = (byte)(startAddress >> 8);
        frame[9] = (byte)(startAddress & 0xFF);
        frame[10] = (byte)(quantity >> 8);
        frame[11] = (byte)(quantity & 0xFF);
        return frame;
    }
}
