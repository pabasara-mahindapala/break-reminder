namespace BreakReminder;

/// <summary>Hidden window that receives a single global hotkey.</summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 1;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyWindow() => CreateHandle(new CreateParams());

    /// <summary>Registers a hotkey such as "Control, Alt, B". Returns false if it is invalid or taken.</summary>
    public bool Register(string hotkey)
    {
        if (!Enum.TryParse<Keys>(hotkey, ignoreCase: true, out var keys)) return false;

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        if (keys.HasFlag(Keys.Control)) modifiers |= NativeMethods.MOD_CONTROL;
        if (keys.HasFlag(Keys.Alt)) modifiers |= NativeMethods.MOD_ALT;
        if (keys.HasFlag(Keys.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
        var key = keys & Keys.KeyCode;
        if (key == Keys.None) return false;

        _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers, (uint)key);
        return _registered;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam == HotkeyId) Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registered) NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        DestroyHandle();
    }
}
