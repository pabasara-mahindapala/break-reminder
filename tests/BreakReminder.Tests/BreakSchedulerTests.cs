using BreakReminder;

namespace BreakReminder.Tests;

public class BreakSchedulerTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Busy = TimeSpan.Zero;

    private static SchedulerConfig Config(TimeSpan? jitter = null) => new(
        new Dictionary<ReminderKind, ReminderConfig>
        {
            [ReminderKind.Posture] = new(true, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(5)),
            [ReminderKind.Eye] = new(true, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(1)),
            [ReminderKind.Movement] = new(true, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5)),
            [ReminderKind.LongBreak] = new(true, TimeSpan.FromHours(2), TimeSpan.FromMinutes(15)),
        },
        ActiveThreshold: TimeSpan.FromSeconds(60),
        FullResetIdle: TimeSpan.FromMinutes(30),
        Snooze: TimeSpan.FromMinutes(5),
        UnmuteDelay: TimeSpan.FromMinutes(1),
        PostureJitter: jitter ?? TimeSpan.Zero);

    // Runs one-second ticks and returns the first reminder shown.
    private static ReminderKind? Run(BreakScheduler s, TimeSpan duration, TimeSpan idle, bool muted = false)
    {
        ReminderKind? shown = null;
        for (var t = TimeSpan.Zero; t < duration; t += Second)
            shown ??= s.Tick(Second, idle, muted);
        return shown;
    }

    [Fact]
    public void PostureFiresFirstAfterActiveUse()
    {
        var s = new BreakScheduler(Config());
        Assert.Null(Run(s, TimeSpan.FromMinutes(15) - Second, Busy));
        Assert.Equal(ReminderKind.Posture, s.Tick(Second, Busy, false));
    }

    [Fact]
    public void IdleTimeDoesNotCount()
    {
        var s = new BreakScheduler(Config());
        Run(s, TimeSpan.FromMinutes(10), Busy);
        Run(s, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(2));
        Assert.Equal(TimeSpan.FromMinutes(20), s.Remaining(ReminderKind.Movement));
    }

    [Fact]
    public void IdleResetsOnlyRemindersWithLowerThresholds()
    {
        var s = new BreakScheduler(Config());
        Run(s, TimeSpan.FromMinutes(10), Busy);
        s.Tick(Second, TimeSpan.FromMinutes(2), false);
        Assert.Equal(TimeSpan.FromMinutes(20), s.Remaining(ReminderKind.Eye));
        Assert.Equal(TimeSpan.FromMinutes(20), s.Remaining(ReminderKind.Movement));
    }

    [Fact]
    public void LongIdleResetsEverything()
    {
        var s = new BreakScheduler(Config());
        Run(s, TimeSpan.FromMinutes(100), Busy);
        s.Tick(Second, TimeSpan.FromMinutes(30), false);
        Assert.Equal(TimeSpan.FromHours(2), s.Remaining(ReminderKind.LongBreak));
    }

    [Fact]
    public void LargestDueReminderWinsAndResetsSmallerOnes()
    {
        var t = new BreakScheduler(Config());
        // Keep a popup showing so posture and eye are both overdue when movement comes due.
        t.ForceShow(ReminderKind.Posture);
        Run(t, TimeSpan.FromMinutes(30), Busy);
        t.Hide(ReminderKind.Posture);
        Assert.Equal(ReminderKind.Movement, t.Tick(Second, Busy, false));
        t.Complete(ReminderKind.Movement);
        Assert.Equal(TimeSpan.FromMinutes(20), t.Remaining(ReminderKind.Eye));
        Assert.Equal(TimeSpan.FromMinutes(15), t.Remaining(ReminderKind.Posture));
        Assert.Equal(TimeSpan.FromMinutes(90) - Second, t.Remaining(ReminderKind.LongBreak));
    }

    [Fact]
    public void SnoozeRefiresAfterSnoozeActiveTime()
    {
        var s = new BreakScheduler(Config());
        // The eye break would come due at the same moment and win.
        s.SetEnabled(ReminderKind.Eye, false);
        Assert.Equal(ReminderKind.Posture, Run(s, TimeSpan.FromMinutes(15), Busy));
        s.Snooze(ReminderKind.Posture);
        Assert.Null(Run(s, TimeSpan.FromMinutes(5) - Second, Busy));
        Assert.Equal(ReminderKind.Posture, s.Tick(Second, Busy, false));
    }

    [Fact]
    public void DueReminderIsHeldWhileMutedAndShownAfterUnmuteDelay()
    {
        var s = new BreakScheduler(Config());
        Assert.Null(Run(s, TimeSpan.FromMinutes(16), Busy, muted: true));
        Assert.Null(Run(s, TimeSpan.FromMinutes(1), Busy));
        Assert.Equal(ReminderKind.Posture, s.Tick(Second, Busy, false));
    }

    [Fact]
    public void NothingIsShownWhileIdle()
    {
        var s = new BreakScheduler(Config());
        Run(s, TimeSpan.FromMinutes(15), Busy);
        s.Hide(ReminderKind.Posture);
        Assert.Null(s.Tick(Second, TimeSpan.FromSeconds(90), false));
    }

    [Fact]
    public void IdleThroughShowingReminderClearsIt()
    {
        var s = new BreakScheduler(Config());
        Run(s, TimeSpan.FromMinutes(15), Busy);
        Assert.Equal(ReminderKind.Posture, s.Showing);
        s.Tick(Second, TimeSpan.FromMinutes(5), false);
        Assert.Null(s.Showing);
    }

    [Fact]
    public void PostureJitterStaysInRange()
    {
        var jitter = TimeSpan.FromMinutes(3);
        for (int seed = 0; seed < 50; seed++)
        {
            var s = new BreakScheduler(Config(jitter), new Random(seed));
            var due = s.Remaining(ReminderKind.Posture);
            Assert.InRange(due, TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(18));
        }
    }

    [Fact]
    public void PostureCuesRotate()
    {
        var cues = new PostureCues(startIndex: PostureCues.All.Count - 1);
        Assert.Equal(PostureCues.All[^1], cues.Next());
        Assert.Equal(PostureCues.All[0], cues.Next());
        Assert.Equal(1, cues.Index);
    }
}
