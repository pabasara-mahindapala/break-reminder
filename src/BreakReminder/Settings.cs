using System.Text.Json;

namespace BreakReminder;

public sealed class ReminderSettings
{
    public bool Enabled { get; set; } = true;
    public double IntervalMinutes { get; set; }
    public double IdleResetSeconds { get; set; }
}

/// <summary>User settings, stored as JSON in %APPDATA%\BreakReminder\settings.json.</summary>
public sealed class Settings
{
    public ReminderSettings Eye { get; set; } = new() { IntervalMinutes = 20, IdleResetSeconds = 300 };
    public ReminderSettings Movement { get; set; } = new() { IntervalMinutes = 30, IdleResetSeconds = 300 };
    public ReminderSettings LongBreak { get; set; } = new() { IntervalMinutes = 120, IdleResetSeconds = 900 };
    public ReminderSettings Posture { get; set; } = new() { IntervalMinutes = 15, IdleResetSeconds = 300 };

    public double PostureJitterSeconds { get; set; } = 180;
    public double ActiveThresholdSeconds { get; set; } = 300;
    public double FullResetIdleMinutes { get; set; } = 30;
    public double SnoozeMinutes { get; set; } = 5;
    public double UnmuteDelaySeconds { get; set; } = 60;

    public int EyeBreakSeconds { get; set; } = 20;
    public int AutoHideSeconds { get; set; } = 20;
    public int PostureAutoHideSeconds { get; set; } = 15;

    public string MuteHotkey { get; set; } = "Control, Alt, B";
    public bool AutoMuteNotificationState { get; set; } = true;
    public bool AutoMuteFullscreen { get; set; } = true;
    public bool AutoMuteScreenShare { get; set; } = true;

    // Window titles that indicate an active screen share (case-insensitive substring match).
    public List<string> ScreenShareWindowTitles { get; set; } =
    [
        "You are screen sharing",
        "zoom share toolbar",
        "Sharing control bar",
        "Sharing toolbar",
    ];

    public ReminderSettings For(ReminderKind kind) => kind switch
    {
        ReminderKind.Eye => Eye,
        ReminderKind.Movement => Movement,
        ReminderKind.LongBreak => LongBreak,
        _ => Posture,
    };

    /// <summary>Builds the scheduler config; <paramref name="scale"/> shortens every duration for testing.</summary>
    public SchedulerConfig ToSchedulerConfig(double scale = 1)
    {
        TimeSpan Min(double m) => TimeSpan.FromMinutes(m * scale);
        TimeSpan Sec(double s) => TimeSpan.FromSeconds(s * scale);
        return new SchedulerConfig(
            BreakScheduler.Kinds.ToDictionary(k => k, k =>
            {
                var r = For(k);
                return new ReminderConfig(r.Enabled, Min(r.IntervalMinutes), Sec(r.IdleResetSeconds));
            }),
            ActiveThreshold: Sec(ActiveThresholdSeconds),
            FullResetIdle: Min(FullResetIdleMinutes),
            Snooze: Min(SnoozeMinutes),
            UnmuteDelay: Sec(UnmuteDelaySeconds),
            PostureJitter: Sec(PostureJitterSeconds));
    }
}

/// <summary>Small persisted state that is not user configuration.</summary>
public sealed class AppState
{
    public int PostureCueIndex { get; set; }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BreakReminder");

    public static string SettingsPath => Path.Combine(Directory, "settings.json");
    private static string StatePath => Path.Combine(Directory, "state.json");

    /// <summary>Loads settings, writing defaults if the file is missing. Returns null on a parse error.</summary>
    public static Settings? LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = new Settings();
            Save(SettingsPath, defaults);
            return defaults;
        }
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath), Options) ?? new Settings();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void SaveSettings(Settings settings) => Save(SettingsPath, settings);

    public static AppState LoadState()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize<AppState>(File.ReadAllText(StatePath), Options) ?? new AppState()
                : new AppState();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return new AppState();
        }
    }

    public static void SaveState(AppState state) => Save(StatePath, state);

    private static void Save<T>(string path, T value)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }
}
