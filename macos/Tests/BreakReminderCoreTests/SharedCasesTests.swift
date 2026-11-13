import Foundation
import XCTest
@testable import BreakReminderCore

/// Runs the scheduler scenarios shared with the Windows test suite.
final class SharedCasesTests: XCTestCase {
    private static let fixtureURL = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()  // BreakReminderCoreTests
        .deletingLastPathComponent()  // Tests
        .deletingLastPathComponent()  // macos
        .deletingLastPathComponent()  // repository root
        .appendingPathComponent("shared/scheduler-cases.json")

    func testSharedScenarios() throws {
        let root = try XCTUnwrap(
            JSONSerialization.jsonObject(with: Data(contentsOf: Self.fixtureURL)) as? [String: Any])
        let config = try readConfig(XCTUnwrap(root["config"] as? [String: Any]))
        let cases = try XCTUnwrap(root["cases"] as? [[String: Any]])
        XCTAssertFalse(cases.isEmpty)

        for testCase in cases {
            let name = testCase["name"] as? String ?? "?"
            let s = BreakScheduler(config: config)
            for step in try XCTUnwrap(testCase["steps"] as? [[String: Any]]) {
                try run(step, on: s, caseName: name)
            }
        }
    }

    private func run(_ step: [String: Any], on s: BreakScheduler, caseName name: String) throws {
        let op = step["op"] as? String ?? ""
        func kind() throws -> ReminderKind { try XCTUnwrap(optionalKind(step["kind"]), name) }

        switch op {
        case "tick":
            let idle = number(step["idle"])
            let muted = step["muted"] as? Bool ?? false
            var shown: ReminderKind?
            for _ in 0..<Int(number(step["seconds"])) {
                let result = s.tick(elapsed: 1, idle: idle, muted: muted)
                if shown == nil { shown = result }
            }
            if step.keys.contains("shows") {
                XCTAssertEqual(shown, optionalKind(step["shows"]), name)
            }
        case "complete": s.complete(try kind())
        case "snooze": s.snooze(try kind())
        case "hide": s.hide(try kind())
        case "forceShow": s.forceShow(try kind())
        case "setEnabled": s.setEnabled(try kind(), step["enabled"] as? Bool ?? false)
        case "expectRemaining": XCTAssertEqual(s.remaining(try kind()), number(step["seconds"]), name)
        case "expectShowing": XCTAssertEqual(s.showing, optionalKind(step["kind"]), name)
        default: XCTFail("Unknown op '\(op)' in '\(name)'")
        }
    }

    private func readConfig(_ c: [String: Any]) throws -> SchedulerConfig {
        let reminders = try XCTUnwrap(c["reminders"] as? [String: [String: Any]])
        var map: [ReminderKind: ReminderConfig] = [:]
        for kind in ReminderKind.allCases {
            let r = try XCTUnwrap(reminders[kind.name])
            map[kind] = ReminderConfig(enabled: r["enabled"] as? Bool ?? false,
                                       interval: number(r["intervalSeconds"]),
                                       idleReset: number(r["idleResetSeconds"]))
        }
        return SchedulerConfig(
            reminders: map,
            activeThreshold: number(c["activeThresholdSeconds"]),
            fullResetIdle: number(c["fullResetIdleSeconds"]),
            snooze: number(c["snoozeSeconds"]),
            unmuteDelay: number(c["unmuteDelaySeconds"]),
            postureJitter: 0)
    }

    private func number(_ value: Any?) -> Double { (value as? NSNumber)?.doubleValue ?? 0 }

    private func optionalKind(_ value: Any?) -> ReminderKind? {
        (value as? String).flatMap(ReminderKind.init(name:))
    }
}
