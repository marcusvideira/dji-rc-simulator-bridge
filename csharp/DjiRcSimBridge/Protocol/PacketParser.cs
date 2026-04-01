using System.Buffers.Binary;

namespace DjiRcSimBridge.Protocol;

/// <summary>
/// Parses raw DUML packet bytes into typed controller state.
/// All methods are pure (no side effects) and operate on spans for zero-allocation parsing.
/// </summary>
internal static class PacketParser
{
    // Stick calibration
    private const int StickCenter = 1024;
    private const int StickRawHalfRange = 660;
    private const int StickDeadzone = 1;
    private const int StickEffectiveRange = StickRawHalfRange - StickDeadzone;
    private const short StickMin = short.MinValue; // -32768
    private const short StickMax = short.MaxValue;  // 32767
    private const byte TriggerMax = 255;

    public static SticksState ParseSticks(ReadOnlySpan<byte> data)
    {
        var (lt, rt) = ParseGimbalToTriggers(data[25..27]);

        return new SticksState(
            RightHorizontal: ParseStickValue(data[13..15]),
            RightVertical: ParseStickValue(data[16..18]),
            LeftHorizontal: ParseStickValue(data[22..24]),
            LeftVertical: ParseStickValue(data[19..21]),
            GimbalLeftTrigger: lt,
            GimbalRightTrigger: rt
        );
    }

    public static ButtonsState? ParseButtons(ReadOnlySpan<byte> data)
    {
        // Payload starts at byte 11, last 2 bytes are CRC
        var payloadLength = data.Length - 11 - 2;
        if (payloadLength <= DumlConstants.ButtonPayloadByte)
            return null;

        var payload = data[11..^2];
        var btnByte = payload[DumlConstants.ButtonPayloadByte];
        var modeByte = payload[DumlConstants.ModePayloadByte];

        var mode = modeByte switch
        {
            DumlConstants.ModeCine => FlightMode.Cine,
            DumlConstants.ModeNormal => FlightMode.Normal,
            _ => FlightMode.Sport,
        };

        return new ButtonsState(
            CameraShoot: (btnByte & DumlConstants.CameraShootMask) != 0,
            Fn: (btnByte & DumlConstants.FnMask) != 0,
            CameraSwap: (btnByte & DumlConstants.CameraSwapMask) != 0,
            Rth: (btnByte & DumlConstants.RthMask) != 0,
            Mode: mode
        );
    }

    private static short ParseStickValue(ReadOnlySpan<byte> raw)
    {
        var centered = ApplyDeadzone(
            BinaryPrimitives.ReadUInt16LittleEndian(raw) - StickCenter
        );
        var value = centered * StickMax / StickEffectiveRange;
        return (short)Math.Clamp(value, StickMin, StickMax);
    }

    private static (byte Left, byte Right) ParseGimbalToTriggers(ReadOnlySpan<byte> raw)
    {
        var centered = ApplyDeadzone(
            BinaryPrimitives.ReadUInt16LittleEndian(raw) - StickCenter
        );
        var value = centered * StickMax / StickEffectiveRange;

        if (value < 0)
        {
            var lt = (byte)Math.Min(TriggerMax, Math.Abs(value) * TriggerMax / StickMax);
            return (lt, 0);
        }

        var rt = (byte)Math.Min(TriggerMax, value * TriggerMax / StickMax);
        return (0, rt);
    }

    private static int ApplyDeadzone(int centered)
    {
        if (Math.Abs(centered) <= StickDeadzone)
            return 0;
        return centered > 0
            ? centered - StickDeadzone
            : centered + StickDeadzone;
    }
}
