using Microsoft.Win32;

namespace BreakReminder;

/// <summary>Reports keyboard/mouse idle time and flags session locks and sleep.</summary>
internal sealed class ActivityTracker : IDisposable
{
    private static readonly TimeSpan LockedIdle = TimeSpan.FromDays(1);

    // SystemEvents may raise on another thread, so handlers only set flags read by the UI timer.
    private volatile bool _locked;
    private volatile bool _resetRequested;

    public ActivityTracker()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public TimeSpan Idle => _locked ? LockedIdle : NativeMethods.GetIdleTime();

    /// <summary>True once after the session was locked or the machine went to sleep.</summary>
    public bool ConsumeResetRequest()
    {
        if (!_resetRequested) return false;
        _resetRequested = false;
        return true;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                _locked = true;
                _resetRequested = true;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                _locked = false;
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) _resetRequested = true;
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
