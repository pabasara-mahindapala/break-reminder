# Break Reminder

A small Windows tray app that reminds you to rest your eyes, move around, take longer breaks, and check your posture. It is based on how you actually use the computer, not a fixed timer.

- Only active use counts. Pauses under 5 minutes, such as reading or thinking, count as screen time.
- Being away long enough counts as the break. Locking the PC, sleep, or 30 minutes idle resets all reminders.
- The popup is small, never takes focus, and is hidden from screen shares.
- Reminders mute with one click or a hotkey, and mute themselves during presentations, fullscreen apps, and screen sharing.

## Reminders

| Reminder | After active use | Idle that counts as the break |
|---|---|---|
| Posture check (rotating cue) | 15 min ± 3 min | 5 min |
| Eye break (20-20-20, 20 s countdown) | 20 min | 5 min |
| Movement break | 30 min | 5 min |
| Long break | 2 h | 15 min |

When several reminders are due, only the largest is shown. Completing it resets the smaller ones.

## Usage

- **Done** resets the reminder. **Snooze** brings it back after 5 more minutes of active use.
- A break popup that is ignored hides after 20 s and comes back after 5 minutes. Posture checks hide after 15 s.
- If you don't touch the keyboard or mouse during the eye-break countdown, the break counts as done.
- Left-click the tray icon or press **Ctrl+Alt+B** to mute or unmute. The tray menu also has timed mute options.
- The tray icon turns grey while muted. Hover over it to see the time left for each reminder.
- A reminder that comes due while muted is shown about 1 minute after unmuting.

## Settings

`%APPDATA%\BreakReminder\settings.json` is created on first run. Open it from the tray menu, then restart the app to apply changes. Every interval, idle threshold, the hotkey, and each auto-mute check can be changed there.

Screen-share detection matches window titles in `ScreenShareWindowTitles`. Add a title there if your meeting app isn't detected.

Turn on **Start with Windows** in the tray menu to launch at login.

## Build

Requires Windows 10/11 and the [.NET SDK](https://dotnet.microsoft.com/download) 9.0.200 or later, which is needed to read the `.slnx` solution file. The app targets .NET 8, and running it needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet test
dotnet publish src/BreakReminder -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o publish
publish\BreakReminder.exe
```

Run with `--fast` to make every duration 20 times shorter for testing.

## License

[MIT](LICENSE)
