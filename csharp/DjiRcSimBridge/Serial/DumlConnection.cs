using System.IO.Ports;
using DjiRcSimBridge.Protocol;

namespace DjiRcSimBridge.Serial;

/// <summary>
/// Manages serial communication with the DJI RC using the DUML protocol.
/// Handles packet framing, sends commands, and reads response packets.
/// </summary>
public sealed class DumlConnection : IDisposable
{
    private readonly SerialPort _port;
    private readonly DumlPacketBuilder _builder = new();

    public DumlConnection(SerialPort port)
    {
        _port = port;
    }

    public string PortName => _port.PortName;

    public int BytesAvailable => _port.BytesToRead;

    public void Send(byte cmdId, ReadOnlySpan<byte> payload = default)
    {
        var packet = _builder.Build(cmdId, payload);
        _port.Write(packet, 0, packet.Length);
    }

    /// <summary>
    /// Read one DUML packet from the serial port.
    /// Returns null if no valid sync byte is available or read times out.
    /// </summary>
    public byte[]? ReadPacket()
    {
        int syncByte;
        try
        {
            syncByte = _port.ReadByte();
        }
        catch (TimeoutException)
        {
            return null;
        }

        if (syncByte != DumlConstants.SyncByte)
            return null;

        // Read 2-byte length field
        var header = new byte[2];
        if (ReadExact(header, 0, 2) < 2)
            return null;

        var length = (header[0] | ((header[1] & 0x03) << 8));
        if (length < 4 || length > DumlConstants.MaxPacketLength)
            return null;

        // Allocate full packet buffer and fill it
        var packet = new byte[length];
        packet[0] = DumlConstants.SyncByte;
        packet[1] = header[0];
        packet[2] = header[1];

        var remaining = length - 3;
        if (remaining > 0 && ReadExact(packet, 3, remaining) < remaining)
            return null;

        return packet;
    }

    private int ReadExact(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            int read;
            try
            {
                read = _port.Read(buffer, offset + totalRead, count - totalRead);
            }
            catch (TimeoutException)
            {
                break;
            }

            if (read == 0)
                break;

            totalRead += read;
        }

        return totalRead;
    }

    public void Dispose()
    {
        if (_port.IsOpen)
            _port.Close();
        _port.Dispose();
    }
}
