import Foundation

/// Manual mute with an optional expiry.
public final class MuteState {
    private var until: Date?

    public init() {}

    public var isMuted: Bool {
        if let until, Date() >= until { self.until = nil }
        return until != nil
    }

    public func mute(for duration: TimeInterval) { until = Date().addingTimeInterval(duration) }

    public func muteIndefinitely() { until = .distantFuture }

    public func unmute() { until = nil }

    public func toggle() {
        if isMuted { unmute() } else { muteIndefinitely() }
    }

    public func describe() -> String {
        guard let until else { return "Not muted" }
        if until == .distantFuture { return "Muted until unmuted" }
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm"
        return "Muted until \(formatter.string(from: until))"
    }
}
