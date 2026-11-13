import Foundation

/// Declaration order is priority order: a later kind wins and resets earlier ones.
public enum ReminderKind: Int, CaseIterable, Comparable {
    case posture, eye, movement, longBreak

    /// Name used in settings and shared test fixtures.
    public var name: String {
        switch self {
        case .posture: return "Posture"
        case .eye: return "Eye"
        case .movement: return "Movement"
        case .longBreak: return "LongBreak"
        }
    }

    public init?(name: String) {
        guard let kind = Self.allCases.first(where: { $0.name == name }) else { return nil }
        self = kind
    }

    public static func < (a: Self, b: Self) -> Bool { a.rawValue < b.rawValue }
}

public struct ReminderConfig {
    public var enabled: Bool
    public var interval: TimeInterval
    public var idleReset: TimeInterval

    public init(enabled: Bool, interval: TimeInterval, idleReset: TimeInterval) {
        self.enabled = enabled
        self.interval = interval
        self.idleReset = idleReset
    }
}

public struct SchedulerConfig {
    public var reminders: [ReminderKind: ReminderConfig]
    public var activeThreshold: TimeInterval
    public var fullResetIdle: TimeInterval
    public var snooze: TimeInterval
    public var unmuteDelay: TimeInterval
    public var postureJitter: TimeInterval

    public init(reminders: [ReminderKind: ReminderConfig], activeThreshold: TimeInterval,
                fullResetIdle: TimeInterval, snooze: TimeInterval, unmuteDelay: TimeInterval,
                postureJitter: TimeInterval) {
        self.reminders = reminders
        self.activeThreshold = activeThreshold
        self.fullResetIdle = fullResetIdle
        self.snooze = snooze
        self.unmuteDelay = unmuteDelay
        self.postureJitter = postureJitter
    }
}

/// Counts active time per reminder and decides which reminder is due.
public final class BreakScheduler {
    private let config: SchedulerConfig
    private let random: () -> Double
    private var active: [ReminderKind: TimeInterval] = [:]
    private var due: [ReminderKind: TimeInterval] = [:]
    private var enabled: [ReminderKind: Bool] = [:]
    private var wasMuted = false
    private var unmuteDelayLeft: TimeInterval = 0

    /// The reminder currently on screen, if any.
    public private(set) var showing: ReminderKind?

    /// `random` returns a value in [0, 1) and is used for posture jitter.
    public init(config: SchedulerConfig, random: @escaping () -> Double = { Double.random(in: 0..<1) }) {
        self.config = config
        self.random = random
        for kind in ReminderKind.allCases {
            enabled[kind] = config.reminders[kind]?.enabled ?? false
            reset(kind)
        }
    }

    public func isEnabled(_ kind: ReminderKind) -> Bool { enabled[kind] ?? false }

    public func setEnabled(_ kind: ReminderKind, _ value: Bool) {
        enabled[kind] = value
        reset(kind)
        if showing == kind { showing = nil }
    }

    public func remaining(_ kind: ReminderKind) -> TimeInterval {
        max(0, due[kind, default: 0] - active[kind, default: 0])
    }

    /// Advances the counters and returns a reminder to show now, if any.
    public func tick(elapsed: TimeInterval, idle: TimeInterval, muted: Bool) -> ReminderKind? {
        let isActive = idle < config.activeThreshold
        if isActive {
            for kind in ReminderKind.allCases where isEnabled(kind) {
                active[kind, default: 0] += elapsed
            }
        } else {
            applyIdle(idle)
        }

        if muted {
            wasMuted = true
            return nil
        }
        if wasMuted {
            wasMuted = false
            unmuteDelayLeft = config.unmuteDelay
        }
        if unmuteDelayLeft > 0 {
            unmuteDelayLeft -= elapsed
            return nil
        }
        if !isActive || showing != nil { return nil }

        var dueKind: ReminderKind?
        for kind in ReminderKind.allCases where isEnabled(kind) && active[kind, default: 0] >= due[kind, default: 0] {
            dueKind = kind
        }
        showing = dueKind
        return dueKind
    }

    /// The user took the break: resets it and every lower-priority reminder.
    public func complete(_ kind: ReminderKind) {
        for k in ReminderKind.allCases where k <= kind { reset(k) }
        showing = nil
    }

    public func snooze(_ kind: ReminderKind) {
        due[kind] = active[kind, default: 0] + config.snooze
        showing = nil
    }

    /// Removes the reminder from screen but keeps it due.
    public func hide(_ kind: ReminderKind) {
        if showing == kind { showing = nil }
    }

    /// Marks a reminder as showing regardless of its counter.
    public func forceShow(_ kind: ReminderKind) { showing = kind }

    public func resetAll() {
        for kind in ReminderKind.allCases { reset(kind) }
        showing = nil
    }

    private func applyIdle(_ idle: TimeInterval) {
        if idle >= config.fullResetIdle {
            resetAll()
            return
        }
        for kind in ReminderKind.allCases where idle >= (config.reminders[kind]?.idleReset ?? .infinity) {
            reset(kind)
            if showing == kind { showing = nil }
        }
    }

    private func reset(_ kind: ReminderKind) {
        var interval = config.reminders[kind]?.interval ?? 0
        if kind == .posture && config.postureJitter > 0 {
            interval += (random() * 2 - 1) * config.postureJitter
        }
        active[kind] = 0
        due[kind] = interval
    }
}
