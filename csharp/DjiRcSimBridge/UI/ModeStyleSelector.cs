using DjiRcSimBridge.Gamepad;
using Spectre.Console;
using Color = Spectre.Console.Color;
using Style = Spectre.Console.Style;

namespace DjiRcSimBridge.UI;

/// <summary>
/// Interactive selector for mode switch behavior using Spectre.Console.
/// </summary>
public static class ModeStyleSelector
{
    private static readonly Dictionary<ModeStyle, string> Labels = new()
    {
        [ModeStyle.Pulse] = "D-pad Left/Up/Right per mode — pulse on change",
        [ModeStyle.Single] = "D-pad Down on any mode change — pulse",
        [ModeStyle.Hold] = "D-pad Left/Up/Right per mode — held while in mode",
    };

    private static readonly Dictionary<ModeStyle, string> Descriptions = new()
    {
        [ModeStyle.Pulse] = "Sends a brief D-pad press matching the mode when you flip the switch",
        [ModeStyle.Single] = "Sends a brief D-pad Down press on any mode change",
        [ModeStyle.Hold] = "Holds the D-pad direction as long as the switch is in that position",
    };

    public static ModeStyle Ask(ModeStyle defaultValue)
    {
        ConsoleUI.PrintBanner();
        AnsiConsole.WriteLine();

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<ModeStyle>()
                .Title("[bold yellow]Mode Switch Style[/]")
                .AddChoices(ModeStyle.Pulse, ModeStyle.Single, ModeStyle.Hold)
                .UseConverter(style =>
                {
                    var label = Labels[style];
                    var desc = Descriptions[style];
                    return $"{label}\n  [dim italic]{desc}[/]";
                })
                .HighlightStyle(new Style(Color.Green, decoration: Decoration.Bold))
                .WrapAround()
        );

        return selected;
    }
}
