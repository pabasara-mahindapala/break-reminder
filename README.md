# Break Reminder

A small Windows tray app that reminds you to rest your eyes, move, take longer breaks, and check your posture. It counts only time you are actively using the computer. An idle period long enough to count as the break resets that reminder. Locking the PC, sleep, or 30 minutes idle resets all of them.

Pauses shorter than 5 minutes (reading, thinking, watching) count as screen time.

| Reminder | After active use | Idle that counts as the break |
|---|---|---|
| Posture check (rotating cue) | 15 min ± 3 min | 5 min |
| Eye break (20-20-20, with a 20 s countdown) | 20 min | 5 min |
| Movement break | 30 min | 5 min |
| Long break | 2 h | 15 min |

When several reminders are due, only the largest is shown, and completing it resets the smaller ones.

## Popup
- Appears in the bottom-right corner, never takes focus, and is hidden from screen capture and screen shares.
- **Done** resets the reminder. **Snooze** brings it back after 5 more minutes of active use.
- A break popup that is ignored hides after 20 s and comes back after 5 minutes. Posture checks hide after 15 s.
- If you don't touch the keyboard or mouse during the eye-break countdown, the break counts as done.

## Muting
- Left-click the tray icon or press **Ctrl+Alt+B** to toggle mute. The tray menu also has *Mute for 30 minutes / 1 hour / until unmuted*.
- The app also mutes itself during presentations, for fullscreen apps, and when a screen-share toolbar window is visible. Share detection matches window titles listed in `ScreenShareWindowTitles`; add titles there if your meeting app isn't detected.
- A reminder that comes due while muted is shown about 1 minute after unmuting.
- The icon turns grey while muted, and hovering over it shows the time left for each reminder.

## Settings
`%APPDATA%\BreakReminder\settings.json` is created on first run. Use *Open settings file* in the tray menu, then restart the app to apply changes. *Start with Windows* in the tray menu adds the app to the per-user Run key.

## Build and run
```
dotnet test
dotnet publish src/BreakReminder -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o publish
publish\BreakReminder.exe          # normal
publish\BreakReminder.exe --fast   # every duration 20x shorter, for testing
```
Requires the .NET 8 Desktop Runtime.
