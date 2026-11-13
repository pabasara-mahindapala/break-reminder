import AppKit

// Exit if another copy is already running.
if let bundleID = Bundle.main.bundleIdentifier,
   NSRunningApplication.runningApplications(withBundleIdentifier: bundleID).count > 1 {
    exit(0)
}

let app = NSApplication.shared
app.setActivationPolicy(.accessory)
// --fast shortens every interval 20x (eye break after 1 minute) for manual testing.
let delegate = AppDelegate(scale: CommandLine.arguments.contains("--fast") ? 1.0 / 20 : 1)
app.delegate = delegate
app.run()
