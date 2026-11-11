using System.Text;

namespace BreakReminder;

/// <summary>Detects presentations, fullscreen apps and screen sharing.</summary>
internal sealed class AutoMuteDetector(Settings settings)
{
    private static readonly HashSet<string> ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd"];

    /// <summary>The reason to mute, or null when reminders may be shown.</summary>
    public string? Check()
    {
        if (settings.AutoMuteNotificationState && IsBusyState()) return "presenting or fullscreen";
        if (settings.AutoMuteFullscreen && IsForegroundFullscreen()) return "fullscreen";
        if (settings.AutoMuteScreenShare && IsScreenSharing()) return "screen sharing";
        return null;
    }

    private static bool IsBusyState() =>
        NativeMethods.SHQueryUserNotificationState(out int state) == 0 &&
        state is NativeMethods.QUNS_BUSY
            or NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN
            or NativeMethods.QUNS_PRESENTATION_MODE;

    private static bool IsForegroundFullscreen()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        var cls = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, cls, cls.Capacity);
        if (ShellClasses.Contains(cls.ToString())) return false;

        if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return false;
        var info = new NativeMethods.MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>(),
        };
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return false;

        var m = info.rcMonitor;
        return rect.Left <= m.Left && rect.Top <= m.Top && rect.Right >= m.Right && rect.Bottom >= m.Bottom;
    }

    private bool IsScreenSharing()
    {
        var titles = settings.ScreenShareWindowTitles;
        if (titles.Count == 0) return false;

        bool found = false;
        var text = new StringBuilder(256);
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            text.Clear();
            if (NativeMethods.GetWindowText(hwnd, text, text.Capacity) == 0) return true;
            var title = text.ToString();
            found = titles.Any(t => title.Contains(t, StringComparison.OrdinalIgnoreCase));
            return !found;
        }, IntPtr.Zero);
        return found;
    }
}
