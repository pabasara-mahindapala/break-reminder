import Foundation

public struct ReminderSettings: Codable {
    public var enabled: Bool
    public var intervalMinutes: Double
    public var idleResetSeconds: Double

    public init(enabled: Bool = true, intervalMinutes: Double, idleResetSeconds: Double) {
        self.enabled = enabled
        self.intervalMinutes = intervalMinutes
        self.idleResetSeconds = idleResetSeconds
    }

    enum CodingKeys: String, CodingKey {
        case enabled = "Enabled", intervalMinutes = "IntervalMinutes", idleResetSeconds = "IdleResetSeconds"
    }
}

/// User settings, stored as JSON with the same keys as the Windows app.
public struct Settings: Codable {
    public var eye = ReminderSettings(intervalMinutes: 20, idleResetSeconds: 300)
    public var movement = ReminderSettings(intervalMinutes: 30, idleResetSeconds: 300)
    public var longBreak = ReminderSettings(intervalMinutes: 120, idleResetSeconds: 900)
    public var posture = ReminderSettings(intervalMinutes: 15, idleResetSeconds: 300)

    public var postureJitterSeconds: Double = 180
    public var activeThresholdSeconds: Double = 300
    public var fullResetIdleMinutes: Double = 30
    public var snoozeMinutes: Double = 5
    public var unmuteDelaySeconds: Double = 60

    public var eyeBreakSeconds = 20
    public var autoHideSeconds = 20
    public var postureAutoHideSeconds = 15

    public var muteHotkey = "Control, Option, B"
    public var autoMuteInCall = true
    public var autoMuteFullscreen = true

    public init() {}

    enum CodingKeys: String, CodingKey {
        case eye = "Eye", movement = "Movement", longBreak = "LongBreak", posture = "Posture"
        case postureJitterSeconds = "PostureJitterSeconds", activeThresholdSeconds = "ActiveThresholdSeconds"
        case fullResetIdleMinutes = "FullResetIdleMinutes", snoozeMinutes = "SnoozeMinutes"
        case unmuteDelaySeconds = "UnmuteDelaySeconds", eyeBreakSeconds = "EyeBreakSeconds"
        case autoHideSeconds = "AutoHideSeconds", postureAutoHideSeconds = "PostureAutoHideSeconds"
        case muteHotkey = "MuteHotkey", autoMuteInCall = "AutoMuteInCall", autoMuteFullscreen = "AutoMuteFullscreen"
    }

    // Missing keys keep their defaults so older settings files still load.
    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        func value<T: Decodable>(_ key: CodingKeys, _ fallback: T) throws -> T {
            try c.decodeIfPresent(T.self, forKey: key) ?? fallback
        }
        let d = Settings()
        eye = try value(.eye, d.eye)
        movement = try value(.movement, d.movement)
        longBreak = try value(.longBreak, d.longBreak)
        posture = try value(.posture, d.posture)
        postureJitterSeconds = try value(.postureJitterSeconds, d.postureJitterSeconds)
        activeThresholdSeconds = try value(.activeThresholdSeconds, d.activeThresholdSeconds)
        fullResetIdleMinutes = try value(.fullResetIdleMinutes, d.fullResetIdleMinutes)
        snoozeMinutes = try value(.snoozeMinutes, d.snoozeMinutes)
        unmuteDelaySeconds = try value(.unmuteDelaySeconds, d.unmuteDelaySeconds)
        eyeBreakSeconds = try value(.eyeBreakSeconds, d.eyeBreakSeconds)
        autoHideSeconds = try value(.autoHideSeconds, d.autoHideSeconds)
        postureAutoHideSeconds = try value(.postureAutoHideSeconds, d.postureAutoHideSeconds)
        muteHotkey = try value(.muteHotkey, d.muteHotkey)
        autoMuteInCall = try value(.autoMuteInCall, d.autoMuteInCall)
        autoMuteFullscreen = try value(.autoMuteFullscreen, d.autoMuteFullscreen)
    }

    public subscript(kind: ReminderKind) -> ReminderSettings {
        get {
            switch kind {
            case .eye: return eye
            case .movement: return movement
            case .longBreak: return longBreak
            case .posture: return posture
            }
        }
        set {
            switch kind {
            case .eye: eye = newValue
            case .movement: movement = newValue
            case .longBreak: longBreak = newValue
            case .posture: posture = newValue
            }
        }
    }

    /// Builds the scheduler config; `scale` shortens every duration for testing.
    public func schedulerConfig(scale: Double = 1) -> SchedulerConfig {
        var reminders: [ReminderKind: ReminderConfig] = [:]
        for kind in ReminderKind.allCases {
            let r = self[kind]
            reminders[kind] = ReminderConfig(enabled: r.enabled, interval: r.intervalMinutes * 60 * scale,
                                             idleReset: r.idleResetSeconds * scale)
        }
        return SchedulerConfig(
            reminders: reminders,
            activeThreshold: activeThresholdSeconds * scale,
            fullResetIdle: fullResetIdleMinutes * 60 * scale,
            snooze: snoozeMinutes * 60 * scale,
            unmuteDelay: unmuteDelaySeconds * scale,
            postureJitter: postureJitterSeconds * scale)
    }
}

/// Small persisted state that is not user configuration.
public struct AppState: Codable {
    public var postureCueIndex = 0

    public init() {}

    enum CodingKeys: String, CodingKey { case postureCueIndex = "PostureCueIndex" }
}

public enum SettingsStore {
    public static let directory = FileManager.default
        .urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        .appendingPathComponent("BreakReminder", isDirectory: true)

    public static var settingsURL: URL { directory.appendingPathComponent("settings.json") }
    private static var stateURL: URL { directory.appendingPathComponent("state.json") }

    /// Loads settings, writing defaults if the file is missing. Returns nil on a parse error.
    public static func loadSettings() -> Settings? {
        guard let data = try? Data(contentsOf: settingsURL) else {
            let defaults = Settings()
            save(defaults, to: settingsURL)
            return defaults
        }
        return try? JSONDecoder().decode(Settings.self, from: data)
    }

    public static func saveSettings(_ settings: Settings) { save(settings, to: settingsURL) }

    public static func loadState() -> AppState {
        guard let data = try? Data(contentsOf: stateURL),
              let state = try? JSONDecoder().decode(AppState.self, from: data) else { return AppState() }
        return state
    }

    public static func saveState(_ state: AppState) { save(state, to: stateURL) }

    private static func save<T: Encodable>(_ value: T, to url: URL) {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try? encoder.encode(value).write(to: url, options: .atomic)
    }
}
