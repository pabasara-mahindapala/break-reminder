using System.Text.Json;
using BreakReminder;

namespace BreakReminder.Tests;

/// <summary>Runs the scheduler scenarios shared with the macOS test suite.</summary>
public class SharedCasesTests
{
    private static readonly JsonElement Root =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scheduler-cases.json"))).RootElement;

    public static IEnumerable<object[]> CaseNames() =>
        Root.GetProperty("cases").EnumerateArray().Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Scenario(string name)
    {
        var testCase = Root.GetProperty("cases").EnumerateArray().First(c => c.GetProperty("name").GetString() == name);
        var s = new BreakScheduler(ReadConfig(Root.GetProperty("config")));

        foreach (var step in testCase.GetProperty("steps").EnumerateArray())
        {
            var op = step.GetProperty("op").GetString();
            switch (op)
            {
                case "tick":
                    var idle = TimeSpan.FromSeconds(Number(step, "idle"));
                    bool muted = step.TryGetProperty("muted", out var m) && m.GetBoolean();
                    ReminderKind? shown = null;
                    for (int i = 0; i < Number(step, "seconds"); i++)
                        shown ??= s.Tick(TimeSpan.FromSeconds(1), idle, muted);
                    if (step.TryGetProperty("shows", out var expected))
                        Assert.Equal(Kind(expected), shown);
                    break;
                case "complete": s.Complete(Kind(step.GetProperty("kind"))!.Value); break;
                case "snooze": s.Snooze(Kind(step.GetProperty("kind"))!.Value); break;
                case "hide": s.Hide(Kind(step.GetProperty("kind"))!.Value); break;
                case "forceShow": s.ForceShow(Kind(step.GetProperty("kind"))!.Value); break;
                case "setEnabled":
                    s.SetEnabled(Kind(step.GetProperty("kind"))!.Value, step.GetProperty("enabled").GetBoolean());
                    break;
                case "expectRemaining":
                    Assert.Equal(TimeSpan.FromSeconds(Number(step, "seconds")),
                        s.Remaining(Kind(step.GetProperty("kind"))!.Value));
                    break;
                case "expectShowing": Assert.Equal(Kind(step.GetProperty("kind")), s.Showing); break;
                default: throw new InvalidOperationException($"Unknown op '{op}' in '{name}'");
            }
        }
    }

    private static SchedulerConfig ReadConfig(JsonElement c)
    {
        TimeSpan Sec(JsonElement e, string key) => TimeSpan.FromSeconds(Number(e, key));
        var reminders = c.GetProperty("reminders");
        return new SchedulerConfig(
            BreakScheduler.Kinds.ToDictionary(k => k, k =>
            {
                var r = reminders.GetProperty(k.ToString());
                return new ReminderConfig(r.GetProperty("enabled").GetBoolean(),
                    Sec(r, "intervalSeconds"), Sec(r, "idleResetSeconds"));
            }),
            ActiveThreshold: Sec(c, "activeThresholdSeconds"),
            FullResetIdle: Sec(c, "fullResetIdleSeconds"),
            Snooze: Sec(c, "snoozeSeconds"),
            UnmuteDelay: Sec(c, "unmuteDelaySeconds"),
            PostureJitter: TimeSpan.Zero);
    }

    private static double Number(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) ? v.GetDouble() : 0;

    private static ReminderKind? Kind(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : Enum.Parse<ReminderKind>(e.GetString()!);
}
