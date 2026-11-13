namespace BreakReminder;

// Declaration order is priority order: a later kind wins and resets earlier ones.
public enum ReminderKind { Posture, Eye, Movement, LongBreak }

public sealed record ReminderConfig(bool Enabled, TimeSpan Interval, TimeSpan IdleReset);

public sealed record SchedulerConfig(
    IReadOnlyDictionary<ReminderKind, ReminderConfig> Reminders,
    TimeSpan ActiveThreshold,
    TimeSpan FullResetIdle,
    TimeSpan Snooze,
    TimeSpan UnmuteDelay,
    TimeSpan PostureJitter);

/// <summary>Counts active time per reminder and decides which reminder is due.</summary>
public sealed class BreakScheduler
{
    public static readonly ReminderKind[] Kinds = Enum.GetValues<ReminderKind>();

    private readonly SchedulerConfig _config;
    private readonly Random _random;
    private readonly Dictionary<ReminderKind, TimeSpan> _active = new();
    private readonly Dictionary<ReminderKind, TimeSpan> _due = new();
    private readonly Dictionary<ReminderKind, bool> _enabled = new();
    private bool _wasMuted;
    private TimeSpan _unmuteDelayLeft;

    public BreakScheduler(SchedulerConfig config, Random? random = null)
    {
        _config = config;
        _random = random ?? new Random();
        foreach (var kind in Kinds)
        {
            _enabled[kind] = config.Reminders[kind].Enabled;
            Reset(kind);
        }
    }

    /// <summary>The reminder currently on screen, if any.</summary>
    public ReminderKind? Showing { get; private set; }

    public bool IsEnabled(ReminderKind kind) => _enabled[kind];

    public void SetEnabled(ReminderKind kind, bool enabled)
    {
        _enabled[kind] = enabled;
        Reset(kind);
        if (Showing == kind) Showing = null;
    }

    public TimeSpan Remaining(ReminderKind kind)
    {
        var left = _due[kind] - _active[kind];
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>Advances the counters and returns a reminder to show now, if any.</summary>
    public ReminderKind? Tick(TimeSpan elapsed, TimeSpan idle, bool muted)
    {
        bool active = idle < _config.ActiveThreshold;
        if (active)
        {
            foreach (var kind in Kinds)
                if (_enabled[kind]) _active[kind] += elapsed;
        }
        else
        {
            ApplyIdle(idle);
        }

        if (muted)
        {
            _wasMuted = true;
            return null;
        }
        if (_wasMuted)
        {
            _wasMuted = false;
            _unmuteDelayLeft = _config.UnmuteDelay;
        }
        if (_unmuteDelayLeft > TimeSpan.Zero)
        {
            _unmuteDelayLeft -= elapsed;
            return null;
        }
        if (!active || Showing != null) return null;

        ReminderKind? due = null;
        foreach (var kind in Kinds)
            if (_enabled[kind] && _active[kind] >= _due[kind]) due = kind;

        Showing = due;
        return due;
    }

    /// <summary>The user took the break: resets it and every lower-priority reminder.</summary>
    public void Complete(ReminderKind kind)
    {
        foreach (var k in Kinds)
            if (k <= kind) Reset(k);
        Showing = null;
    }

    public void Snooze(ReminderKind kind)
    {
        _due[kind] = _active[kind] + _config.Snooze;
        Showing = null;
    }

    /// <summary>Removes the reminder from screen but keeps it due.</summary>
    public void Hide(ReminderKind kind)
    {
        if (Showing == kind) Showing = null;
    }

    /// <summary>Marks a reminder as showing regardless of its counter.</summary>
    public void ForceShow(ReminderKind kind) => Showing = kind;

    public void ResetAll()
    {
        foreach (var kind in Kinds) Reset(kind);
        Showing = null;
    }

    private void ApplyIdle(TimeSpan idle)
    {
        if (idle >= _config.FullResetIdle)
        {
            ResetAll();
            return;
        }
        foreach (var kind in Kinds)
        {
            if (idle < _config.Reminders[kind].IdleReset) continue;
            Reset(kind);
            if (Showing == kind) Showing = null;
        }
    }

    private void Reset(ReminderKind kind)
    {
        var interval = _config.Reminders[kind].Interval;
        if (kind == ReminderKind.Posture && _config.PostureJitter > TimeSpan.Zero)
        {
            var jitter = _config.PostureJitter.TotalSeconds;
            interval += TimeSpan.FromSeconds((_random.NextDouble() * 2 - 1) * jitter);
        }
        _active[kind] = TimeSpan.Zero;
        _due[kind] = interval;
    }
}
