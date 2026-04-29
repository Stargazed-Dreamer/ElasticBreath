using Microsoft.Win32;

namespace ElasticBreath.App.Services;

public sealed class SessionMonitor : IDisposable
{
    public SessionMonitor()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public event EventHandler<bool>? SessionLockChanged;

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            SessionLockChanged?.Invoke(this, true);
            return;
        }

        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            SessionLockChanged?.Invoke(this, false);
        }
    }
}
