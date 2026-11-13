// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "BreakReminder",
    platforms: [.macOS(.v13)],
    targets: [
        .target(name: "BreakReminderCore"),
        .executableTarget(name: "BreakReminder", dependencies: ["BreakReminderCore"]),
        .testTarget(name: "BreakReminderCoreTests", dependencies: ["BreakReminderCore"]),
    ]
)
