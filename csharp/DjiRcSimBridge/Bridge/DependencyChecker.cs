using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DjiRcSimBridge.Bridge;

/// <summary>
/// Checks that required system dependencies are installed before the bridge runs.
/// Shows actionable instructions if anything is missing.
/// </summary>
public static class DependencyChecker
{
    private const string ViGEmDriverName = "ViGEmBus";
    private const string ViGEmDownloadUrl = "https://github.com/nefarius/ViGEmBus/releases";
    private const string DjiAssistantUrl = "https://www.dji.com/downloads/softwares/dji-assistant-2-consumer-drones-series";

    public record DependencyStatus(bool ViGEmInstalled, bool DjiDriverInstalled, bool ViGEmDllFound);

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static DependencyStatus Check()
    {
        return new DependencyStatus(
            ViGEmInstalled: IsViGEmBusInstalled(),
            DjiDriverInstalled: IsDjiDriverInstalled(),
            ViGEmDllFound: IsViGEmClientDllAvailable()
        );
    }

    /// <summary>
    /// Shows a dialog if dependencies are missing. Returns true if all OK to proceed.
    /// </summary>
    public static bool EnsureOrPrompt()
    {
        var status = Check();

        if (status.ViGEmInstalled && status.ViGEmDllFound)
            return true;

        var missing = new List<string>();

        if (!status.ViGEmInstalled)
            missing.Add($"- ViGEm Bus Driver (virtual gamepad driver)\n  Download: {ViGEmDownloadUrl}");

        if (!status.ViGEmDllFound)
            missing.Add("- ViGEmClient.dll not found next to executable");

        if (!status.DjiDriverInstalled)
            missing.Add($"- DJI USB VCOM Driver (for RC serial communication)\n  Install DJI Assistant 2: {DjiAssistantUrl}");

        var message =
            "The following dependencies are missing:\n\n" +
            string.Join("\n\n", missing) +
            "\n\nPlease install the missing dependencies and restart the application." +
            "\n\nWould you like to open the download page?";

        var result = MessageBox.Show(
            message,
            "DJI RC-N3 Simulator Bridge - Missing Dependencies",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (result == DialogResult.Yes)
        {
            if (!status.ViGEmInstalled)
                OpenUrl(ViGEmDownloadUrl);
            else if (!status.DjiDriverInstalled)
                OpenUrl(DjiAssistantUrl);
        }

        return false;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsViGEmBusInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\ViGEmBus");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsDjiDriverInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
            if (key is null) return false;

            foreach (var vidPid in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(vidPid);
                if (subKey is null) continue;

                foreach (var serial in subKey.GetSubKeyNames())
                {
                    using var deviceKey = subKey.OpenSubKey(serial);
                    var name = deviceKey?.GetValue("FriendlyName")?.ToString() ?? "";
                    if (name.Contains("DJI", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch { }

        return false;
    }

    private static bool IsViGEmClientDllAvailable()
    {
        var dllPath = Path.Combine(AppContext.BaseDirectory, "ViGEmClient.dll");
        return File.Exists(dllPath);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }
}
