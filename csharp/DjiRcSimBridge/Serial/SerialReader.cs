using System.Threading.Channels;
using DjiRcSimBridge.Protocol;

namespace DjiRcSimBridge.Serial;

/// <summary>
/// Polls the RC for stick/button data and writes parsed updates to a channel.
/// Runs in a dedicated thread so serial I/O never blocks the gamepad loop.
/// </summary>
public sealed class SerialReader
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(8);

    private readonly DumlConnection _conn;
    private readonly ChannelWriter<object> _writer;
    private readonly CancellationToken _ct;

    public SerialReader(DumlConnection conn, ChannelWriter<object> writer, CancellationToken ct)
    {
        _conn = conn;
        _writer = writer;
        _ct = ct;
    }

    public void Run()
    {
        while (!_ct.IsCancellationRequested)
        {
            var loopStart = Stopwatch.GetTimestamp();

            try
            {
                _conn.Send(DumlConstants.CmdSticks);
                _conn.Send(DumlConstants.CmdButtons);

                DrainResponses();
            }
            catch (Exception) when (_ct.IsCancellationRequested)
            {
                break;
            }

            SleepRemaining(loopStart, PollInterval);
        }
    }

    private void DrainResponses()
    {
        for (var i = 0; i < 10; i++)
        {
            if (_conn.BytesAvailable == 0)
                break;

            var data = _conn.ReadPacket();
            if (data is null)
                break;

            var cmdId = data.Length > 10 ? data[10] : (byte)0;

            if (data.Length == DumlConstants.SticksPacketLength && cmdId == DumlConstants.CmdSticks)
            {
                _writer.TryWrite(PacketParser.ParseSticks(data));
            }
            else if (data.Length == DumlConstants.ButtonsPacketLength && cmdId == DumlConstants.CmdButtons)
            {
                var state = PacketParser.ParseButtons(data);
                if (state.HasValue)
                    _writer.TryWrite(state.Value);
            }
        }
    }

    private static void SleepRemaining(long startTimestamp, TimeSpan interval)
    {
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        var remaining = interval - elapsed;
        if (remaining > TimeSpan.Zero)
            Thread.Sleep(remaining);
    }
}
