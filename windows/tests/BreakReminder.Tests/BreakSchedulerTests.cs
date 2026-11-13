using BreakReminder;

namespace BreakReminder.Tests;

/// <summary>Platform-specific checks; shared scheduler scenarios live in shared/scheduler-cases.json.</summary>
public class BreakSchedulerTests
{
    private static SchedulerConfig Config(TimeSpan jitter) => new(
        BreakScheduler.Kinds.ToDictionary(k => k, _ => new ReminderConfig(true, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(5))),
        ActiveThreshold: TimeSpan.FromSeconds(60),
        FullResetIdle: TimeSpan.FromMinutes(30),
        Snooze: TimeSpan.FromMinutes(5),
        UnmuteDelay: TimeSpan.FromMinutes(1),
        PostureJitter: jitter);

    [Fact]
    public void PostureJitterStaysInRange()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var s = new BreakScheduler(Config(TimeSpan.FromMinutes(3)), new Random(seed));
            Assert.InRange(s.Remaining(ReminderKind.Posture), TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(18));
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
