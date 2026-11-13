using System.Text;
using Microsoft.Win32;

namespace BreakReminder;

/// <summary>Detects calls, presentations, fullscreen apps and screen sharing.</summary>
internal sealed class AutoMuteDetector(Settings settings)
{
    private const string ConsentStore = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";
    private static readonly HashSet<string> ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd"];

    /// <summary>The reason to mute, or null when reminders may be shown.</summary>
    public string? Check()
    {
        if (settings.AutoMuteInCall && IsInCall()) return "in a call";
        if (settings.AutoMuteNotificationState && IsBusyState()) return "presenting or fullscreen";
        if (settings.AutoMuteFullscreen && IsForegroundFullscreen()) return "fullscreen";
        if (settings.AutoMuteScreenShare && IsScreenSharing()) return "screen sharing";
        return null;
    }

    private static bool IsInCall() => IsDeviceInUse("webcam") || IsDeviceInUse("microphone");

    // Windows records per-app camera/mic use; a started session with no stop time is in use now.
    private static bool IsDeviceInUse(string device)
    {
        using var root = Registry.CurrentUser.OpenSubKey($@"{ConsentStore}\{device}");
        if (root == null) return false;
        if (AnyAppInUse(root)) return true;
        using var nonPackaged = root.OpenSubKey("NonPackaged");
        return nonPackaged != null && AnyAppInUse(nonPackaged);
    }

    private static bool AnyAppInUse(RegistryKey parent)
    {
        foreach (var name in parent.GetSubKeyNames())
        {
            using var app = parent.OpenSubKey(name);
            if (app?.GetValue("LastUsedTimeStart") is long start && start > 0 &&
                app.GetValue("LastUsedTimeStop") is long stop && stop == 0)
                return true;
        }
        return false;
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
