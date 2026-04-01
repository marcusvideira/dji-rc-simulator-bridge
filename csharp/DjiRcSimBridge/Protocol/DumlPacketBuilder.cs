using System.Buffers.Binary;

namespace DjiRcSimBridge.Protocol;

/// <summary>
/// Builds DUML command packets with auto-incrementing sequence numbers.
/// </summary>
internal sealed class DumlPacketBuilder
{
    private ushort _sequence = 0x34EB;

    /// <summary>
    /// Build a complete DUML packet ready to send over serial.
    /// The length field in DUML represents the TOTAL packet size (header + payload + CRC16).
    /// </summary>
    public byte[] Build(byte cmdId, ReadOnlySpan<byte> payload = default)
    {
        // HeaderSize (13) = 11 header bytes + 2 CRC16 bytes
        var totalLength = DumlConstants.HeaderSize + payload.Length;
        var crcOffset = totalLength - 2; // CRC16 goes at the last 2 bytes

        Span<byte> packet = stackalloc byte[totalLength];

        // Sync byte
        packet[0] = DumlConstants.SyncByte;

        // Length field (little-endian, upper nibble = version 0x04)
        packet[1] = (byte)(totalLength & 0xFF);
        packet[2] = (byte)(((totalLength >> 8) & 0x03) | 0x04);

        // Header CRC-8 over first 3 bytes
        packet[3] = DumlChecksum.ComputeHeaderCrc8(packet[..3]);

        // Addressing
        packet[4] = DumlConstants.Source;
        packet[5] = DumlConstants.Target;

        // Sequence number (little-endian)
        BinaryPrimitives.WriteUInt16LittleEndian(packet[6..], _sequence);

        // Command
        packet[8] = DumlConstants.CommandType;
        packet[9] = DumlConstants.CommandSet;
        packet[10] = cmdId;

        // Payload
        payload.CopyTo(packet[11..]);

        // CRC-16 over everything before the CRC itself
        var crc = DumlChecksum.ComputeCrc16(packet[..crcOffset]);
        BinaryPrimitives.WriteUInt16LittleEndian(packet[crcOffset..], crc);

        _sequence++;
        return packet.ToArray();
    }
}
