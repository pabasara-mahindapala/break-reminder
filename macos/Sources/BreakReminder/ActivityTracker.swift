import AppKit
import CoreGraphics

/// Reports keyboard/mouse idle time and flags screen locks, session switches and sleep.
final class ActivityTracker {
    private static let lockedIdle: TimeInterval = 86_400
    private static let anyInput = CGEventType(rawValue: ~0)!

    private var locked = false
    private var resetRequested = false
    private var observers: [(NotificationCenter, NSObjectProtocol)] = []

    init() {
        let distributed = DistributedNotificationCenter.default()
        observe(distributed, Notification.Name("com.apple.screenIsLocked")) { $0.lock() }
        observe(distributed, Notification.Name("com.apple.screenIsUnlocked")) { $0.locked = false }

        let workspace = NSWorkspace.shared.notificationCenter
        observe(workspace, NSWorkspace.sessionDidResignActiveNotification) { $0.lock() }
        observe(workspace, NSWorkspace.sessionDidBecomeActiveNotification) { $0.locked = false }
        observe(workspace, NSWorkspace.willSleepNotification) { $0.resetRequested = true }
    }

    deinit {
        for (center, token) in observers { center.removeObserver(token) }
    }

    var idle: TimeInterval {
        locked ? Self.lockedIdle : CGEventSource.secondsSinceLastEventType(.combinedSessionState, eventType: Self.anyInput)
    }

    /// True once after the screen was locked or the machine went to sleep.
    func consumeResetRequest() -> Bool {
        defer { resetRequested = false }
        return resetRequested
    }

    private func lock() {
        locked = true
        resetRequested = true
    }

    private func observe(_ center: NotificationCenter, _ name: Notification.Name, _ handler: @escaping (ActivityTracker) -> Void) {
        let token = center.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
            if let self { handler(self) }
        }
        observers.append((center, token))
    }
}
