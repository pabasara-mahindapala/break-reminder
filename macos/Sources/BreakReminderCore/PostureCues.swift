/// Rotating posture cues, each describing correct posture.
public struct PostureCues {
    public static let all = [
        "Sit tall: lift the crown of your head toward the ceiling. Don't force your shoulders back.",
        "Back against the chair with lumbar support, and feet flat on the floor.",
        "Keep your ears over your shoulders. Do a few chin tucks if your head has drifted forward.",
        "Relax your shoulders down, not shrugged, with elbows near your sides at about 90°.",
        "The top of the screen should be at or just below eye level, about an arm's length away.",
        "Unclench your jaw, relax your hands, and take one slow breath.",
    ]

    /// Index of the cue returned by the next call to `next()`.
    public private(set) var index: Int

    public init(startIndex: Int) {
        index = abs(startIndex) % Self.all.count
    }

    public mutating func next() -> String {
        let cue = Self.all[index]
        index = (index + 1) % Self.all.count
        return cue
    }
}
