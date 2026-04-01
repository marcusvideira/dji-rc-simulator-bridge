using DjiRcSimBridge.Gamepad;
using DjiRcSimBridge.Protocol;
using Spectre.Console;
using Color = Spectre.Console.Color;
using Panel = Spectre.Console.Panel;

namespace DjiRcSimBridge.UI;

/// <summary>
/// All console output — banner, mapping table, status messages, and debug.
/// </summary>
public static class ConsoleUI
{
    public const string Version = "5.1.0";

    public static void PrintBanner()
    {
        AnsiConsole.Clear();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel("[bold cyan]DJI RC-N3 Simulator Bridge[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Header = new PanelHeader($"v{Version}", Justify.Right),
            Width = 60,
        });
    }

    public static void PrintMappingTable(ModeStyle modeStyle)
    {
        var table = new Table()
        {
            Title = new TableTitle("Gamepad Mapping", new Style(Color.Cyan1, decoration: Decoration.Bold)),
            Border = TableBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Width = 58,
        };

        table.AddColumn(new TableColumn("[bold]RC-N3 Control[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Xbox 360[/]").NoWrap());

        table.AddRow("Left stick", "[green]Left joystick[/]");
        table.AddRow("Right stick", "[green]Right joystick[/]");
        table.AddRow("Gimbal scroll down", "[green]Left Trigger (LT)[/]");
        table.AddRow("Gimbal scroll up", "[green]Right Trigger (RT)[/]");
        table.AddRow("Camera shoot btn", "[green]Right Bumper (RB)[/]");
        table.AddRow("FN button", "[green]Left Bumper (LB)[/]");
        table.AddRow("Camera swap btn", "[green]Y button[/]");
        table.AddRow("RTH button", "[green]Back button[/]");

        switch (modeStyle)
        {
            case ModeStyle.Pulse:
                table.AddRow("Mode: Cinematic", "[green]D-pad Left[/] [dim](pulse)[/]");
                table.AddRow("Mode: Normal", "[green]D-pad Up[/] [dim](pulse)[/]");
                table.AddRow("Mode: Sport", "[green]D-pad Right[/] [dim](pulse)[/]");
                break;
            case ModeStyle.Single:
                table.AddRow("Mode: any change", "[green]D-pad Down[/] [dim](pulse)[/]");
                break;
            case ModeStyle.Hold:
                table.AddRow("Mode: Cinematic", "[green]D-pad Left[/] [dim](held)[/]");
                table.AddRow("Mode: Normal", "[green]D-pad Up[/] [dim](held)[/]");
                table.AddRow("Mode: Sport", "[green]D-pad Right[/] [dim](held)[/]");
                break;
        }

        AnsiConsole.Write(table);
    }

    public static void PrintDebugState(SticksState s, ButtonsState b)
    {
        Console.Write(
            $"\rSticks LH:{s.LeftHorizontal,+6} LV:{s.LeftVertical,+6} " +
            $"RH:{s.RightHorizontal,+6} RV:{s.RightVertical,+6} " +
            $"| LT:{s.GimbalLeftTrigger,3} RT:{s.GimbalRightTrigger,3} " +
            $"| Mode:{b.Mode,-6} " +
            $"| shoot:{b.CameraShoot} fn:{b.Fn} " +
            $"swap:{b.CameraSwap} rth:{b.Rth}"
        );
    }
}
