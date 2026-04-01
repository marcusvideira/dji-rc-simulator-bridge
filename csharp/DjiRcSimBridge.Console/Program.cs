using System.Diagnostics;
using System.Threading.Channels;
using DjiRcSimBridge.Config;
using DjiRcSimBridge.Gamepad;
using DjiRcSimBridge.Protocol;
using DjiRcSimBridge.Serial;
using DjiRcSimBridge.UI;
using Spectre.Console;

namespace DjiRcSimBridge.Console;

internal static class Program
{
    private static readonly TimeSpan GamepadUpdateInterval = TimeSpan.FromMilliseconds(10);

    static int Main(string[] args)
    {
        var port = ParsePort(args);

        using var timer = new TimerResolution();
        ViGEmNative.EnsureLoaded();

        var config = AppConfig.Load();
        ModeStyle modeStyle;
        try
        {
            modeStyle = ModeStyleSelector.Ask(config.ModeStyle);
        }
        catch (NotSupportedException)
        {
            modeStyle = config.ModeStyle;
        }
        config.ModeStyle = modeStyle;
        config.Save();

        using var gamepad = new GamepadOutput(modeStyle);
        Thread.Sleep(1000);

        ConsoleUI.PrintBanner();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [yellow]Scanning serial ports...[/]");

        DumlConnection conn;
        try
        {
            var serialPort = SerialPortDetector.DetectWithDescription(port);
            conn = new DumlConnection(serialPort);
            AnsiConsole.MarkupLine($"  [green]Connected to {conn.PortName}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [bold red]Error: {ex.Message}[/]");
            return 1;
        }

        using (conn)
        {
            AnsiConsole.WriteLine();
            ConsoleUI.PrintMappingTable(modeStyle);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [bold green]RC-N3 emulation started.[/]");
            AnsiConsole.MarkupLine("  [dim]Press Ctrl+C to stop.[/]");
            AnsiConsole.WriteLine();

            conn.Send(DumlConstants.CmdSimMode, [0x01]);

            var channel = Channel.CreateUnbounded<object>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = true }
            );
            using var cts = new CancellationTokenSource();

            System.Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var reader = new SerialReader(conn, channel.Writer, cts.Token);
            var readerThread = new Thread(reader.Run)
            {
                Name = "serial-reader",
                IsBackground = true,
            };
            readerThread.Start();

            RunGamepadLoop(gamepad, channel.Reader, cts.Token);

            cts.Cancel();
            readerThread.Join(TimeSpan.FromSeconds(1));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [yellow]Stopping.[/]");
        return 0;
    }

    private static void RunGamepadLoop(
        GamepadOutput gamepad,
        ChannelReader<object> reader,
        CancellationToken ct)
    {
        (SticksState Sticks, ButtonsState Buttons)? lastDebug = null;

        while (!ct.IsCancellationRequested)
        {
            var loopStart = Stopwatch.GetTimestamp();

            while (reader.TryRead(out var update))
            {
                switch (update)
                {
                    case SticksState sticks:
                        gamepad.Apply(sticks);
                        break;
                    case ButtonsState buttons:
                        gamepad.Apply(buttons);
                        break;
                }
            }

            gamepad.Push();

            var snapshot = gamepad.DebugSnapshot;
            if (snapshot != lastDebug)
            {
                lastDebug = snapshot;
                ConsoleUI.PrintDebugState(snapshot.Sticks, snapshot.Buttons);
            }

            var elapsed = Stopwatch.GetElapsedTime(loopStart);
            var remaining = GamepadUpdateInterval - elapsed;
            if (remaining > TimeSpan.Zero)
                Thread.Sleep(remaining);
        }
    }

    private static string? ParsePort(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "-p" or "--port" && i + 1 < args.Length)
                return args[++i];
        }

        return null;
    }
}
