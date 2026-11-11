using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace BreakReminder;

/// <summary>Owns the tray icon, the activity loop and the reminder popup.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private static readonly TimeSpan MaxTickGap = TimeSpan.FromSeconds(10);

    private readonly Settings _settings;
    private readonly AppState _state;
    private readonly BreakScheduler _scheduler;
    private readonly PostureCues _cues;
    private readonly ActivityTracker _tracker = new();
    private readonly AutoMuteDetector _autoMute;
    private readonly MuteState _mute = new();
    private readonly HotkeyWindow _hotkey = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly (Icon Icon, IntPtr Handle) _activeIcon;
    private readonly (Icon Icon, IntPtr Handle) _mutedIcon;

    private readonly ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _unmuteItem;
    private readonly ToolStripMenuItem _pauseEyeItem;
    private readonly ToolStripMenuItem _startupItem;

    private PopupForm? _popup;
    private string? _autoMuteReason;

    public TrayContext(Settings settings, bool settingsInvalid, double scale)
    {
        _settings = settings;
        _state = SettingsStore.LoadState();
        _cues = new PostureCues(_state.PostureCueIndex);
        _scheduler = new BreakScheduler(settings.ToSchedulerConfig(scale));
        _autoMute = new AutoMuteDetector(settings);

        _activeIcon = CreateIcon(Color.FromArgb(38, 166, 154));
        _mutedIcon = CreateIcon(Color.FromArgb(130, 130, 130));

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Mute for 30 minutes", null, (_, _) => SetMute(() => _mute.MuteFor(TimeSpan.FromMinutes(30))));
        menu.Items.Add("Mute for 1 hour", null, (_, _) => SetMute(() => _mute.MuteFor(TimeSpan.FromHours(1))));
        menu.Items.Add("Mute until unmuted", null, (_, _) => SetMute(_mute.MuteIndefinitely));
        _unmuteItem = new ToolStripMenuItem("Unmute", null, (_, _) => SetMute(_mute.Unmute));
        menu.Items.Add(_unmuteItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Take a break now", null, (_, _) => TakeBreakNow());
        _pauseEyeItem = new ToolStripMenuItem("Pause eye reminders", null, (_, _) => ToggleEyeReminders());
        menu.Items.Add(_pauseEyeItem);
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup());
        menu.Items.Add(_startupItem);
        menu.Items.Add("Open settings file (restart to apply)", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => RefreshMenu();

        _tray = new NotifyIcon
        {
            Icon = _activeIcon.Icon,
            Text = "Break Reminder",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) SetMute(_mute.Toggle);
        };

        _hotkey.Pressed += () => SetMute(_mute.Toggle);
        if (!_hotkey.Register(settings.MuteHotkey))
            Notify($"Couldn't register the mute hotkey \"{settings.MuteHotkey}\". It may be in use by another app.");
        if (settingsInvalid)
            Notify("settings.json couldn't be read, so default settings are in use.");

        _timer.Tick += (_, _) => OnTick();
        _timer.Start();
    }

    private void OnTick()
    {
        var elapsed = _clock.Elapsed;
        _clock.Restart();

        if (_tracker.ConsumeResetRequest())
        {
            ClosePopup();
            _scheduler.ResetAll();
        }

        var idle = _tracker.Idle;
        // A long gap between ticks means the machine slept or stalled; treat it as idle.
        if (elapsed > MaxTickGap && elapsed > idle) idle = elapsed;

        _autoMuteReason = _autoMute.Check();
        bool muted = _mute.IsMuted || _autoMuteReason != null;
        if (muted && _popup != null)
        {
            _scheduler.Hide(_popup.Kind);
            ClosePopup();
        }

        var show = _scheduler.Tick(elapsed, idle, muted);
        if (_popup != null && _scheduler.Showing != _popup.Kind) ClosePopup();
        if (show is { } kind) ShowPopup(kind);

        UpdateTray(muted);
    }

    private void ShowPopup(ReminderKind kind)
    {
        ClosePopup();
        var s = _settings;
        _popup = kind switch
        {
            ReminderKind.Eye => new PopupForm(kind, "Eye break",
                "Look at something about 20 feet (6 m) away. Blink fully a few times.",
                s.EyeBreakSeconds, s.AutoHideSeconds, allowSnooze: true, Color.FromArgb(77, 182, 172), () => _tracker.Idle),
            ReminderKind.Movement => new PopupForm(kind, "Time to move",
                "Stand up and walk or stretch for 2–5 minutes.",
                0, s.AutoHideSeconds, allowSnooze: true, Color.FromArgb(255, 183, 77), () => _tracker.Idle),
            ReminderKind.LongBreak => new PopupForm(kind, "Take a longer break",
                "You've been at the screen for a long stretch. Step away for about 15 minutes.",
                0, s.AutoHideSeconds, allowSnooze: true, Color.FromArgb(239, 108, 98), () => _tracker.Idle),
            _ => new PopupForm(kind, "Posture check", NextCue(),
                0, s.PostureAutoHideSeconds, allowSnooze: false, Color.FromArgb(129, 162, 236), () => _tracker.Idle),
        };
        _popup.Finished += OnPopupFinished;
        _popup.ShowInCorner();
    }

    private string NextCue()
    {
        var cue = _cues.Next();
        _state.PostureCueIndex = _cues.Index;
        SettingsStore.SaveState(_state);
        return cue;
    }

    private void OnPopupFinished(PopupForm popup, PopupResult result)
    {
        if (popup == _popup) _popup = null;
        switch (result)
        {
            case PopupResult.Done:
                _scheduler.Complete(popup.Kind);
                break;
            case PopupResult.TimedOut when popup.Kind == ReminderKind.Posture:
                _scheduler.Complete(popup.Kind);
                break;
            default:
                _scheduler.Snooze(popup.Kind);
                break;
        }
    }

    private void ClosePopup()
    {
        _popup?.CloseSilently();
        _popup = null;
    }

    private void SetMute(Action change)
    {
        change();
        if (_mute.IsMuted && _popup != null)
        {
            _scheduler.Hide(_popup.Kind);
            ClosePopup();
        }
        UpdateTray(_mute.IsMuted || _autoMuteReason != null);
    }

    private void TakeBreakNow()
    {
        if (_popup != null) _scheduler.Hide(_popup.Kind);
        _scheduler.ForceShow(ReminderKind.Movement);
        ShowPopup(ReminderKind.Movement);
    }

    private void ToggleEyeReminders()
    {
        bool enable = !_scheduler.IsEnabled(ReminderKind.Eye);
        _scheduler.SetEnabled(ReminderKind.Eye, enable);
        _settings.Eye.Enabled = enable;
        SettingsStore.SaveSettings(_settings);
        if (!enable && _popup?.Kind == ReminderKind.Eye) ClosePopup();
    }

    private static void ToggleStartup() => Startup.SetEnabled(!Startup.IsEnabled);

    private static void OpenSettings() =>
        Process.Start(new ProcessStartInfo(SettingsStore.SettingsPath) { UseShellExecute = true });

    private void RefreshMenu()
    {
        _statusItem.Text = StatusText().Replace('\n', ' ');
        _unmuteItem.Enabled = _mute.IsMuted;
        _pauseEyeItem.Checked = !_scheduler.IsEnabled(ReminderKind.Eye);
        _startupItem.Checked = Startup.IsEnabled;
    }

    private void UpdateTray(bool muted)
    {
        var icon = muted ? _mutedIcon.Icon : _activeIcon.Icon;
        if (_tray.Icon != icon) _tray.Icon = icon;

        var text = StatusText();
        // NotifyIcon.Text is limited to 127 characters.
        if (text.Length > 127) text = text[..127];
        if (_tray.Text != text) _tray.Text = text;
    }

    private string StatusText()
    {
        if (_mute.IsMuted) return _mute.Describe();
        if (_autoMuteReason != null) return $"Muted ({_autoMuteReason})";

        var parts = new List<string>();
        void Add(ReminderKind kind, string label)
        {
            if (_scheduler.IsEnabled(kind)) parts.Add($"{label} {Format(_scheduler.Remaining(kind))}");
        }
        Add(ReminderKind.Eye, "Eye");
        Add(ReminderKind.Movement, "Move");
        Add(ReminderKind.LongBreak, "Long");
        Add(ReminderKind.Posture, "Posture");
        return "Break Reminder\n" + (parts.Count > 0 ? string.Join(" · ", parts) : "All reminders off");
    }

    private static string Format(TimeSpan t) =>
        t.TotalMinutes < 1 ? "<1m"
        : t.TotalHours >= 1 ? $"{(int)t.TotalHours}h{t.Minutes:00}m"
        : $"{(int)Math.Ceiling(t.TotalMinutes)}m";

    private void Notify(string message) =>
        _tray.ShowBalloonTip(5000, "Break Reminder", message, ToolTipIcon.Warning);

    private static (Icon, IntPtr) CreateIcon(Color color)
    {
        var size = SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = size.Width, h = size.Height;
            using var fill = new SolidBrush(color);
            g.FillEllipse(fill, 0.5f, 0.5f, w - 1.5f, h - 1.5f);
            // A simple eye: white almond with a dark pupil.
            g.FillEllipse(Brushes.White, w * 0.18f, h * 0.32f, w * 0.62f, h * 0.36f);
            using var pupil = new SolidBrush(Color.FromArgb(40, 40, 40));
            g.FillEllipse(pupil, w * 0.38f, h * 0.38f, w * 0.22f, h * 0.24f);
        }
        var handle = bitmap.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        ClosePopup();
        _tray.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Dispose();
            _hotkey.Dispose();
            _tracker.Dispose();
            _activeIcon.Icon.Dispose();
            _mutedIcon.Icon.Dispose();
            NativeMethods.DestroyIcon(_activeIcon.Handle);
            NativeMethods.DestroyIcon(_mutedIcon.Handle);
        }
        base.Dispose(disposing);
    }
}
