import Foundation
import XCTest
@testable import BreakReminderCore

/// Platform-specific checks; shared scheduler scenarios live in shared/scheduler-cases.json.
final class CoreTests: XCTestCase {
    func testPostureJitterStaysInRange() {
        var settings = Settings()
        settings.postureJitterSeconds = 180
        for value in stride(from: 0.0, to: 1.0, by: 0.05) {
            let s = BreakScheduler(config: settings.schedulerConfig(), random: { value })
            XCTAssertGreaterThanOrEqual(s.remaining(.posture), 12 * 60)
            XCTAssertLessThanOrEqual(s.remaining(.posture), 18 * 60)
        }
    }

    func testPostureCuesRotate() {
        var cues = PostureCues(startIndex: PostureCues.all.count - 1)
        XCTAssertEqual(cues.next(), PostureCues.all.last)
        XCTAssertEqual(cues.next(), PostureCues.all.first)
        XCTAssertEqual(cues.index, 1)
    }

    func testSettingsReadWindowsKeysAndFillDefaults() throws {
        let json = #"{ "Eye": { "Enabled": false, "IntervalMinutes": 25, "IdleResetSeconds": 120 }, "ScreenShareWindowTitles": [] }"#
        let settings = try JSONDecoder().decode(Settings.self, from: Data(json.utf8))
        XCTAssertFalse(settings.eye.enabled)
        XCTAssertEqual(settings.eye.intervalMinutes, 25)
        XCTAssertEqual(settings.movement.intervalMinutes, 30)
        XCTAssertEqual(settings.muteHotkey, "Control, Option, B")
    }
}
