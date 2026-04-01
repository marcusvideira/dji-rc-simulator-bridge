using DjiRcSimBridge.Protocol;
using static DjiRcSimBridge.Gamepad.ViGEmNative;

namespace DjiRcSimBridge.Gamepad;

/// <summary>
/// Manages the virtual Xbox 360 gamepad via the native ViGEmClient.dll C API.
/// Accumulates state updates and pushes to the driver on each <see cref="Push"/> call.
/// </summary>
public sealed class GamepadOutput : IDisposable
{
    private static readonly TimeSpan ModePulseDuration = TimeSpan.FromMilliseconds(150);

    private static readonly Dictionary<FlightMode, XusbButton> ModeButtons = new()
    {
        [FlightMode.Cine] = XusbButton.DpadLeft,
        [FlightMode.Normal] = XusbButton.DpadUp,
        [FlightMode.Sport] = XusbButton.DpadRight,
    };

    private readonly IntPtr _client;
    private readonly IntPtr _target;
    private readonly ModeStyle _modeStyle;

    private SticksState _sticks;
    private ButtonsState _buttons = new(false, false, false, false, FlightMode.Normal);
    private FlightMode _previousMode = FlightMode.Normal;
    private long _pulseEndTimestamp;
    private bool _disposed;

    public GamepadOutput(ModeStyle modeStyle)
    {
        _modeStyle = modeStyle;

        _client = Alloc();
        if (_client == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate ViGEm client.");

        CheckError(Connect(_client), "connect");

        _target = TargetX360Alloc();
        if (_target == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate Xbox 360 target.");

        CheckError(TargetAdd(_client, _target), "target_add");

        // Send initial zeroed report so the OS recognizes the device
        Push();
    }

    public void Apply(SticksState sticks) => _sticks = sticks;

    public void Apply(ButtonsState buttons)
    {
        if (buttons.Mode != _previousMode)
        {
            _pulseEndTimestamp = Stopwatch.GetTimestamp()
                + (long)(ModePulseDuration.TotalSeconds * Stopwatch.Frequency);
            _previousMode = buttons.Mode;
        }

        _buttons = buttons;
    }

    public void Push()
    {
        var report = new XusbReport
        {
            ThumbLX = _sticks.LeftHorizontal,
            ThumbLY = _sticks.LeftVertical,
            ThumbRX = _sticks.RightHorizontal,
            ThumbRY = _sticks.RightVertical,
            LeftTrigger = _sticks.GimbalLeftTrigger,
            RightTrigger = _sticks.GimbalRightTrigger,
            Buttons = BuildButtons(),
        };

        TargetX360Update(_client, _target, report);
    }

    public (SticksState Sticks, ButtonsState Buttons) DebugSnapshot => (_sticks, _buttons);

    private ushort BuildButtons()
    {
        var flags = (XusbButton)0;

        if (_buttons.CameraShoot) flags |= XusbButton.RightShoulder;
        if (_buttons.Fn) flags |= XusbButton.LeftShoulder;
        if (_buttons.CameraSwap) flags |= XusbButton.Y;
        if (_buttons.Rth) flags |= XusbButton.Back;

        flags |= GetModeButtons();

        return (ushort)flags;
    }

    private XusbButton GetModeButtons()
    {
        var isPulsing = Stopwatch.GetTimestamp() < _pulseEndTimestamp;

        return _modeStyle switch
        {
            ModeStyle.Pulse => isPulsing ? ModeButtons[_buttons.Mode] : 0,
            ModeStyle.Single => isPulsing ? XusbButton.DpadDown : 0,
            ModeStyle.Hold => ModeButtons[_buttons.Mode],
            _ => 0,
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_target != IntPtr.Zero)
        {
            TargetRemove(_client, _target);
            TargetFree(_target);
        }

        if (_client != IntPtr.Zero)
        {
            Disconnect(_client);
            Free(_client);
        }
    }
}
