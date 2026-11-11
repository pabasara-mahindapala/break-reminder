namespace BreakReminder;

/// <summary>Manual mute with an optional expiry.</summary>
internal sealed class MuteState
{
    private DateTime? _until;

    public bool IsMuted
    {
        get
        {
            if (_until is { } until && DateTime.Now >= until) _until = null;
            return _until != null;
        }
    }

    public void MuteFor(TimeSpan duration) => _until = DateTime.Now + duration;

    public void MuteIndefinitely() => _until = DateTime.MaxValue;

    public void Unmute() => _until = null;

    public void Toggle()
    {
        if (IsMuted) Unmute();
        else MuteIndefinitely();
    }

    public string Describe() => _until switch
    {
        null => "Not muted",
        { } u when u == DateTime.MaxValue => "Muted until unmuted",
        { } u => $"Muted until {u:HH:mm}",
    };
}
