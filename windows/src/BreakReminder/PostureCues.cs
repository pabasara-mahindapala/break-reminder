namespace BreakReminder;

/// <summary>Rotating posture cues, each describing correct posture.</summary>
public sealed class PostureCues
{
    public static readonly IReadOnlyList<string> All =
    [
        "Sit tall: lift the crown of your head toward the ceiling. Don't force your shoulders back.",
        "Back against the chair with lumbar support, and feet flat on the floor.",
        "Keep your ears over your shoulders. Do a few chin tucks if your head has drifted forward.",
        "Relax your shoulders down, not shrugged, with elbows near your sides at about 90°.",
        "The top of the screen should be at or just below eye level, about an arm's length away.",
        "Unclench your jaw, relax your hands, and take one slow breath.",
    ];

    public PostureCues(int startIndex) => Index = Math.Abs(startIndex) % All.Count;

    /// <summary>Index of the cue returned by the next call to <see cref="Next"/>.</summary>
    public int Index { get; private set; }

    public string Next()
    {
        var cue = All[Index];
        Index = (Index + 1) % All.Count;
        return cue;
    }
}
