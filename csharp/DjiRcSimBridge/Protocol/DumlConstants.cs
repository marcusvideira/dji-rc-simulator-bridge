namespace DjiRcSimBridge.Protocol;

/// <summary>
/// Constants for the DJI DUML (DJI Universal Markup Language) protocol.
/// </summary>
public static class DumlConstants
{
    public const byte SyncByte = 0x55;
    public const byte Source = 0x0A;
    public const byte Target = 0x06;
    public const byte CommandType = 0x40;
    public const byte CommandSet = 0x06;

    // Command IDs
    public const byte CmdSticks = 0x01;
    public const byte CmdSimMode = 0x24;
    public const byte CmdButtons = 0x27;

    // Expected packet lengths
    public const int SticksPacketLength = 38;
    public const int ButtonsPacketLength = 58;

    // Header
    public const int HeaderSize = 13;
    public const byte HeaderCrc8Seed = 0x77;
    public const int MaxPacketLength = 0x3FF;

    // Button payload offsets (within payload starting at byte 11)
    public const int ButtonPayloadByte = 18;
    public const int ModePayloadByte = 17;

    // Button bitmasks
    public const byte CameraShootMask = 0x60;
    public const byte FnMask = 0x02;
    public const byte CameraSwapMask = 0x04;
    public const byte RthMask = 0x80;

    // Mode switch values
    public const byte ModeSport = 0x00;
    public const byte ModeNormal = 0x10;
    public const byte ModeCine = 0x20;
}
