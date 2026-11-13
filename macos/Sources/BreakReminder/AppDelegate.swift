import AppKit
import BreakReminderCore

/// Menu item that runs a closure.
private final class ActionItem: NSMenuItem {
    private let handler: () -> Void

    init(_ title: String, key: String = "", handler: @escaping () -> Void) {
        self.handler = handler
        super.init(title: title, action: #selector(run), keyEquivalent: key)
        target = self
    }

    required init(coder: NSCoder) { fatalError("init(coder:) is not used") }

    @objc private func run() { handler() }
}

/// Owns the menu bar item, the activity loop and the reminder popup.
final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private static let maxTickGap: TimeInterval = 10

    private var settings: Settings
    private let settingsInvalid: Bool
    private var state: AppState
    private let scheduler: BreakScheduler
    private var cues: PostureCues
    private let tracker = ActivityTracker()
    private let autoMute: AutoMuteDetector
    private let mute = MuteState()
    private var hotkey: Hotkey?
    private var statusItem: NSStatusItem!
    private let menu = NSMenu()
    private var timer: Timer?
    private var lastTick = Date()
    private var popup: PopupPanel?
    private var autoMuteReason: String?

    init(scale: Double) {
        let loaded = SettingsStore.loadSettings()
        let settings = loaded ?? Settings()
        let state = SettingsStore.loadState()
        self.settings = settings
        self.state = state
        settingsInvalid = loaded == nil
        scheduler = BreakScheduler(config: settings.schedulerConfig(scale: scale))
        autoMute = AutoMuteDetector(settings: settings)
        cues = PostureCues(startIndex: state.postureCueIndex)
        super.init()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        if let button = statusItem.button {
            button.target = self
            button.action = #selector(statusItemClicked)
            button.sendAction(on: [.leftMouseUp, .rightMouseUp])
        }
        menu.delegate = self

        hotkey = Hotkey(settings.muteHotkey) { [weak self] in self?.setMute { $0.toggle() } }
        if hotkey == nil {
            warn("Couldn't register the mute hotkey \"\(settings.muteHotkey)\". It may be in use by another app.")
        }
        if settingsInvalid {
            warn("settings.json couldn't be read, so default settings are in use.")
        }

        let timer = Timer(timeInterval: 1, repeats: true) { [weak self] _ in self?.onTick() }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
        updateStatusItem(muted: false)
    }

    // MARK: Tick loop

    private func onTick() {
        let now = Date()
        let elapsed = max(0, now.timeIntervalSince(lastTick))
        lastTick = now

        if tracker.consumeResetRequest() {
            closePopup()
            scheduler.resetAll()
        }

        var idle = tracker.idle
        // A long gap between ticks means the machine slept or stalled; treat it as idle.
        if elapsed > Self.maxTickGap && elapsed > idle { idle = elapsed }

        autoMuteReason = autoMute.check()
        let muted = mute.isMuted || autoMuteReason != nil
        if muted, let popup {
            scheduler.hide(popup.kind)
            closePopup()
        }

        let show = scheduler.tick(elapsed: elapsed, idle: idle, muted: muted)
        if let popup, scheduler.showing != popup.kind { closePopup() }
        if let show { showPopup(show) }

        updateStatusItem(muted: muted)
    }

    // MARK: Popup

    private func showPopup(_ kind: ReminderKind) {
        closePopup()
        let idle: () -> TimeInterval = { [tracker] in tracker.idle }
        let panel: PopupPanel
        switch kind {
        case .eye:
            panel = PopupPanel(kind: kind, title: "Eye break",
                               body: "Look at something about 20 feet (6 m) away. Blink fully a few times.",
                               countdownSeconds: settings.eyeBreakSeconds, autoHideSeconds: settings.autoHideSeconds,
                               allowSnooze: true, accent: .systemTeal, idle: idle)
        case .movement:
            panel = PopupPanel(kind: kind, title: "Time to move",
                               body: "Stand up and walk or stretch for 2–5 minutes.",
                               countdownSeconds: 0, autoHideSeconds: settings.autoHideSeconds,
                               allowSnooze: true, accent: .systemOrange, idle: idle)
        case .longBreak:
            panel = PopupPanel(kind: kind, title: "Take a longer break",
                               body: "You've been at the screen for a long stretch. Step away for about 15 minutes.",
                               countdownSeconds: 0, autoHideSeconds: settings.autoHideSeconds,
                               allowSnooze: true, accent: .systemRed, idle: idle)
        case .posture:
            panel = PopupPanel(kind: kind, title: "Posture check", body: nextCue(),
                               countdownSeconds: 0, autoHideSeconds: settings.postureAutoHideSeconds,
                               allowSnooze: false, accent: .systemBlue, idle: idle)
        }
        panel.onFinish = { [weak self] panel, result in self?.popupFinished(panel, result) }
        popup = panel
        panel.showInCorner()
    }

    private func nextCue() -> String {
        let cue = cues.next()
        state.postureCueIndex = cues.index
        SettingsStore.saveState(state)
        return cue
    }

    private func popupFinished(_ panel: PopupPanel, _ result: PopupResult) {
        if panel === popup { popup = nil }
        switch result {
        case .done:
            scheduler.complete(panel.kind)
        case .timedOut where panel.kind == .posture:
            scheduler.complete(panel.kind)
        default:
            scheduler.snooze(panel.kind)
        }
    }

    private func closePopup() {
        popup?.closeSilently()
        popup = nil
    }

    // MARK: Menu bar

    @objc private func statusItemClicked() {
        // Option-click toggles mute; any other click opens the menu.
        if NSEvent.modifierFlags.contains(.option) {
            setMute { $0.toggle() }
            return
        }
        statusItem.menu = menu
        statusItem.button?.performClick(nil)
        statusItem.menu = nil
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        menu.removeAllItems()
        for line in statusLines() {
            let item = NSMenuItem(title: line, action: nil, keyEquivalent: "")
            item.isEnabled = false
            menu.addItem(item)
        }
        menu.addItem(.separator())

        if mute.isMuted {
            menu.addItem(ActionItem("Unmute") { [weak self] in self?.setMute { $0.unmute() } })
        }
        menu.addItem(ActionItem("Mute for 30 minutes") { [weak self] in self?.setMute { $0.mute(for: 30 * 60) } })
        menu.addItem(ActionItem("Mute for 1 hour") { [weak self] in self?.setMute { $0.mute(for: 60 * 60) } })
        menu.addItem(ActionItem("Mute until unmuted") { [weak self] in self?.setMute { $0.muteIndefinitely() } })
        menu.addItem(.separator())

        menu.addItem(ActionItem("Take a break now") { [weak self] in self?.takeBreakNow() })
        let pauseEye = ActionItem("Pause eye reminders") { [weak self] in self?.toggleEyeReminders() }
        pauseEye.state = scheduler.isEnabled(.eye) ? .off : .on
        menu.addItem(pauseEye)
        let login = ActionItem("Start at login") { LoginItem.setEnabled(!LoginItem.isEnabled) }
        login.state = LoginItem.isEnabled ? .on : .off
        menu.addItem(login)
        menu.addItem(ActionItem("Open settings file (restart to apply)") {
            NSWorkspace.shared.open(SettingsStore.settingsURL)
        })
        menu.addItem(.separator())
        menu.addItem(ActionItem("Quit Break Reminder", key: "q") { NSApp.terminate(nil) })
    }

    private func setMute(_ change: (MuteState) -> Void) {
        change(mute)
        if mute.isMuted, let popup {
            scheduler.hide(popup.kind)
            closePopup()
        }
        updateStatusItem(muted: mute.isMuted || autoMuteReason != nil)
    }

    private func takeBreakNow() {
        if let popup { scheduler.hide(popup.kind) }
        scheduler.forceShow(.movement)
        showPopup(.movement)
    }

    private func toggleEyeReminders() {
        let enable = !scheduler.isEnabled(.eye)
        scheduler.setEnabled(.eye, enable)
        settings.eye.enabled = enable
        SettingsStore.saveSettings(settings)
        if !enable && popup?.kind == .eye { closePopup() }
    }

    private func updateStatusItem(muted: Bool) {
        guard let button = statusItem?.button else { return }
        let symbol = muted ? "eye.slash" : "eye"
        if button.image?.accessibilityDescription != symbol {
            let image = NSImage(systemSymbolName: symbol, accessibilityDescription: symbol)
            image?.isTemplate = true
            button.image = image
        }
        button.toolTip = statusLines().joined(separator: "\n")
    }

    private func statusLines() -> [String] {
        if mute.isMuted { return [mute.describe()] }
        if let autoMuteReason { return ["Muted (\(autoMuteReason))"] }

        let labels: [(ReminderKind, String)] = [(.eye, "Eye break"), (.movement, "Movement break"),
                                                (.longBreak, "Long break"), (.posture, "Posture check")]
        let lines = labels.filter { scheduler.isEnabled($0.0) }
            .map { "\($0.1) in \(Self.format(scheduler.remaining($0.0)))" }
        return lines.isEmpty ? ["All reminders off"] : lines
    }

    private static func format(_ t: TimeInterval) -> String {
        if t < 60 { return "<1m" }
        if t >= 3600 { return String(format: "%dh%02dm", Int(t) / 3600, (Int(t) % 3600) / 60) }
        return "\(Int((t / 60).rounded(.up)))m"
    }

    private func warn(_ message: String) {
        let alert = NSAlert()
        alert.messageText = "Break Reminder"
        alert.informativeText = message
        alert.alertStyle = .warning
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }
}
