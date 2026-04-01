namespace DjiRcSimBridge.Gamepad;

/// <summary>
/// How the RC mode switch maps to Xbox d-pad buttons.
/// </summary>
public enum ModeStyle
{
    /// <summary>Brief d-pad press matching the mode on each change.</summary>
    Pulse,

    /// <summary>Brief d-pad Down press on any mode change.</summary>
    Single,

    /// <summary>D-pad direction held as long as the switch is in that position.</summary>
    Hold,
}
