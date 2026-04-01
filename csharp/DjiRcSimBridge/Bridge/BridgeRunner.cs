using System.Threading.Channels;
using DjiRcSimBridge.Gamepad;
using DjiRcSimBridge.Protocol;
using DjiRcSimBridge.Serial;

namespace DjiRcSimBridge.Bridge;

/// <summary>
/// Orchestrates the serial reader, gamepad output, and data flow.
/// Runs on background threads, exposes state for UI observation.
/// </summary>
public sealed class BridgeRunner : IDisposable
{
    private static readonly TimeSpan GamepadUpdateInterval = TimeSpan.FromMilliseconds(10);

    private readonly GamepadOutput _gamepad;
    private readonly DumlConnection _conn;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<object> _channel;
    private Thread? _serialThread;
    private Thread? _gamepadThread;
    private bool _disposed;

    public string PortName => _conn.PortName;
    public bool IsRunning => _serialThread?.IsAlive == true;
    public (SticksState Sticks, ButtonsState Buttons) CurrentState => _gamepad.DebugSnapshot;

    public BridgeRunner(DumlConnection conn, ModeStyle modeStyle)
    {
        _conn = conn;
        _gamepad = new GamepadOutput(modeStyle);
        _channel = Channel.CreateUnbounded<object>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true }
        );
    }

    public void Start()
    {
        // Enable simulator mode on the RC
        _conn.Send(DumlConstants.CmdSimMode, [0x01]);

        // Serial reader thread
        var reader = new SerialReader(_conn, _channel.Writer, _cts.Token);
        _serialThread = new Thread(reader.Run)
        {
            Name = "serial-reader",
            IsBackground = true,
        };
        _serialThread.Start();

        // Gamepad update thread
        _gamepadThread = new Thread(RunGamepadLoop)
        {
            Name = "gamepad-loop",
            IsBackground = true,
        };
        _gamepadThread.Start();
    }

    public void Stop()
    {
        _cts.Cancel();
        _serialThread?.Join(TimeSpan.FromSeconds(1));
        _gamepadThread?.Join(TimeSpan.FromSeconds(1));
    }

    private void RunGamepadLoop()
    {
        var channelReader = _channel.Reader;
        var ct = _cts.Token;

        while (!ct.IsCancellationRequested)
        {
            var loopStart = Stopwatch.GetTimestamp();

            while (channelReader.TryRead(out var update))
            {
                switch (update)
                {
                    case SticksState sticks:
                        _gamepad.Apply(sticks);
                        break;
                    case ButtonsState buttons:
                        _gamepad.Apply(buttons);
                        break;
                }
            }

            _gamepad.Push();

            var elapsed = Stopwatch.GetElapsedTime(loopStart);
            var remaining = GamepadUpdateInterval - elapsed;
            if (remaining > TimeSpan.Zero)
                Thread.Sleep(remaining);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _gamepad.Dispose();
        _conn.Dispose();
        _cts.Dispose();
    }
}
