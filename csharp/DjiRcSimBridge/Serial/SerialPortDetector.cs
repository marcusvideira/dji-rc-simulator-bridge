using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Spectre.Console;

namespace DjiRcSimBridge.Serial;

/// <summary>
/// Auto-detects the DJI USB VCOM serial port or falls back to a user-specified port.
/// Uses Windows Registry to read port descriptions without requiring System.Management.
/// </summary>
public static class SerialPortDetector
{
    private const string DjiPortMarker = "For Protocol";
    private const int BaudRate = 115200;
    private const int ReadTimeoutMs = 5;

    public static SerialPort DetectWithDescription(string? fallbackPort)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var detected = TryDetectViaRegistry();
            if (detected is not null)
                return detected;
        }

        // Fallback: try all available ports
        foreach (var portName in SerialPort.GetPortNames())
        {
            AnsiConsole.MarkupLine($"    [dim]{portName}[/]");
            try
            {
                var port = CreatePort(portName);
                port.Open();
                return port;
            }
            catch
            {
                // Port in use or not accessible — continue
            }
        }

        if (!string.IsNullOrEmpty(fallbackPort))
        {
            AnsiConsole.MarkupLine($"    [yellow]No auto-detect, using fallback: {fallbackPort}[/]");
            var port = CreatePort(fallbackPort);
            port.Open();
            return port;
        }

        throw new InvalidOperationException(
            "No DJI USB VCOM port found. Connect the RC-N3 or use -p COM<n>"
        );
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static SerialPort? TryDetectViaRegistry()
    {
        // Enumerate USB serial devices in the registry to find DJI VCOM description
        try
        {
            using var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
            if (enumKey is null) return null;

            foreach (var vidPid in enumKey.GetSubKeyNames())
            {
                using var vidPidKey = enumKey.OpenSubKey(vidPid);
                if (vidPidKey is null) continue;

                foreach (var serial in vidPidKey.GetSubKeyNames())
                {
                    using var deviceKey = vidPidKey.OpenSubKey(serial);
                    var friendlyName = deviceKey?.GetValue("FriendlyName")?.ToString() ?? "";

                    if (!friendlyName.Contains(DjiPortMarker, StringComparison.OrdinalIgnoreCase))
                        continue;

                    AnsiConsole.MarkupLine($"    [dim]{friendlyName}[/]");

                    // Extract COM port from Device Parameters
                    using var paramsKey = deviceKey?.OpenSubKey("Device Parameters");
                    var portName = paramsKey?.GetValue("PortName")?.ToString();
                    if (string.IsNullOrEmpty(portName)) continue;

                    try
                    {
                        var port = CreatePort(portName);
                        port.Open();
                        return port;
                    }
                    catch
                    {
                        // Port exists but can't be opened — continue
                    }
                }
            }
        }
        catch
        {
            // Registry access failed — fall through to generic detection
        }

        return null;
    }

    private static SerialPort CreatePort(string portName) =>
        new(portName, BaudRate)
        {
            ReadTimeout = ReadTimeoutMs,
            WriteTimeout = 1000,
        };
}
