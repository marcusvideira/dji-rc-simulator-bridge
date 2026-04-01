using DjiRcSimBridge.Bridge;
using DjiRcSimBridge.Gamepad;
using DjiRcSimBridge.GUI;
using DjiRcSimBridge.UI;

namespace DjiRcSimBridge.GuiApp;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var port = ParsePort(args);

        using var timer = new TimerResolution();
        ViGEmNative.EnsureLoaded();

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (!DependencyChecker.EnsureOrPrompt())
            return 1;

        Application.Run(new MainForm(port));
        return 0;
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
