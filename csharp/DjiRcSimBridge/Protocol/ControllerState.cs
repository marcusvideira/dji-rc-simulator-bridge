namespace DjiRcSimBridge.Protocol;

/// <summary>
/// Parsed stick + gimbal positions from a 38-byte DUML response.
/// Immutable value type for lock-free passing between threads.
/// </summary>
public readonly record struct SticksState(
    short RightHorizontal,
    short RightVertical,
    short LeftHorizontal,
    short LeftVertical,
    byte GimbalLeftTrigger,
    byte GimbalRightTrigger
);

/// <summary>
/// Flight mode from the RC's 3-position switch.
/// </summary>
public enum FlightMode
{
    Cine,
    Normal,
    Sport,
}

/// <summary>
/// Parsed button + mode state from a 58-byte DUML response.
/// Immutable value type for lock-free passing between threads.
/// </summary>
public readonly record struct ButtonsState(
    bool CameraShoot,
    bool Fn,
    bool CameraSwap,
    bool Rth,
    FlightMode Mode
);
