using System.Runtime.InteropServices;

namespace DjiRcSimBridge.UI;

/// <summary>
/// Sets Windows multimedia timer resolution to 1ms for accurate Thread.Sleep().
/// Implements IDisposable to ensure timeEndPeriod is always called.
/// </summary>
public sealed class TimerResolution : IDisposable
{
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeEndPeriod(uint uMilliseconds);

    private bool _active;

    public TimerResolution()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            timeBeginPeriod(1);
            _active = true;
        }
    }

    public void Dispose()
    {
        if (_active)
        {
            timeEndPeriod(1);
            _active = false;
        }
    }
}
