# Break Reminder

A small tray / menu bar app for Windows and macOS that reminds you to rest your eyes, move around, take longer breaks, and check your posture. It is based on how you actually use the computer, not a fixed timer.

- Only active use counts. Pauses under 5 minutes, such as reading or thinking, count as screen time.
- Being away long enough counts as the break. Locking the screen, sleep, or 30 minutes idle resets all reminders.
- The popup is small, never takes focus, and opts out of screen capture.
- Reminders mute with one click or a hotkey, and mute themselves during calls (camera or mic in use) and fullscreen apps.

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
- A reminder that comes due while muted is shown about 1 minute after unmuting.

| | Windows | macOS |
|---|---|---|
| Toggle mute | Left-click the tray icon, or **Ctrl+Alt+B** | Option-click the menu bar icon, or **⌃⌥B** |
| Menu | Right-click the tray icon | Click the menu bar icon |
| Time left | Hover over the icon | Shown at the top of the menu |
| Muted icon | Grey | Crossed-out eye |
| Settings file | `%APPDATA%\BreakReminder\settings.json` | `~/Library/Application Support/BreakReminder/settings.json` |
| Launch at login | **Start with Windows** in the menu | **Start at login** in the menu |

Settings are created on first run. Open the file from the menu, then restart the app to apply changes. Every interval, idle threshold, the hotkey, and each auto-mute check can be changed there.

Auto-mute also covers presentations and screen sharing on Windows. Screen sharing is detected by window title (`ScreenShareWindowTitles`); add a title there if your meeting app isn't detected. On macOS, hiding the popup from screen shares is best effort on macOS 15 and later, so the call auto-mute does most of the work.

## Build

### Windows

Requires Windows 10/11 and the [.NET SDK](https://dotnet.microsoft.com/download) 9.0.200 or later, which is needed to read the `.slnx` solution file. The app targets .NET 8, and running it needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

```
cd windows
dotnet test
dotnet publish src/BreakReminder -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o publish
publish\BreakReminder.exe
```

### macOS

Requires macOS 13 or later and Xcode 15 or later.

```
cd macos
swift test
scripts/bundle.sh
open dist/BreakReminder.app
```

The app is not notarized. On first launch, macOS blocks it: open **System Settings → Privacy & Security** and click **Open Anyway**, or run `xattr -dr com.apple.quarantine dist/BreakReminder.app`.

Run either app with `--fast` to make every duration 20 times shorter for testing (on macOS: `open dist/BreakReminder.app --args --fast`).

## Repository layout

- `windows/` – C# WinForms app and tests
- `macos/` – Swift menu bar app and tests
- `shared/scheduler-cases.json` – scheduler scenarios that both test suites run, so the two apps behave the same

## License

[MIT](LICENSE)
