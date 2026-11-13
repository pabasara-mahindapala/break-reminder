import AppKit
import BreakReminderCore
import CoreAudio
import CoreMediaIO

/// Detects calls (camera or microphone in use) and fullscreen apps. Needs no permissions.
struct AutoMuteDetector {
    let settings: Settings

    /// The reason to mute, or nil when reminders may be shown.
    func check() -> String? {
        if settings.autoMuteInCall && (isCameraInUse() || isMicrophoneInUse()) { return "in a call" }
        if settings.autoMuteFullscreen && isFrontmostAppFullscreen() { return "fullscreen" }
        return nil
    }

    private func isFrontmostAppFullscreen() -> Bool {
        guard let app = NSWorkspace.shared.frontmostApplication,
              app.processIdentifier != ProcessInfo.processInfo.processIdentifier,
              let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID)
                as? [[String: Any]] else { return false }

        let screenSizes = NSScreen.screens.map(\.frame.size)
        // Owner and bounds are readable without Screen Recording permission; titles are not.
        return windows.contains { window in
            guard (window[kCGWindowOwnerPID as String] as? pid_t) == app.processIdentifier,
                  (window[kCGWindowLayer as String] as? Int) == 0,
                  let boundsDict = window[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: boundsDict as CFDictionary) else { return false }
            return screenSizes.contains { bounds.width >= $0.width && bounds.height >= $0.height }
        }
    }

    private func isCameraInUse() -> Bool {
        var address = CMIOObjectPropertyAddress(
            mSelector: CMIOObjectPropertySelector(kCMIOHardwarePropertyDevices),
            mScope: CMIOObjectPropertyScope(kCMIOObjectPropertyScopeGlobal),
            mElement: CMIOObjectPropertyElement(kCMIOObjectPropertyElementMain))
        let system = CMIOObjectID(kCMIOObjectSystemObject)

        var size: UInt32 = 0
        guard CMIOObjectGetPropertyDataSize(system, &address, 0, nil, &size) == 0, size > 0 else { return false }
        var devices = [CMIOObjectID](repeating: 0, count: Int(size) / MemoryLayout<CMIOObjectID>.size)
        var used: UInt32 = 0
        guard CMIOObjectGetPropertyData(system, &address, 0, nil, size, &used, &devices) == 0 else { return false }

        address.mSelector = CMIOObjectPropertySelector(kCMIODevicePropertyDeviceIsRunningSomewhere)
        return devices.contains { device in
            var running: UInt32 = 0
            var dataUsed: UInt32 = 0
            let status = CMIOObjectGetPropertyData(device, &address, 0, nil,
                                                   UInt32(MemoryLayout<UInt32>.size), &dataUsed, &running)
            return status == 0 && running != 0
        }
    }

    private func isMicrophoneInUse() -> Bool {
        var address = AudioObjectPropertyAddress(
            mSelector: kAudioHardwarePropertyDevices,
            mScope: kAudioObjectPropertyScopeGlobal,
            mElement: kAudioObjectPropertyElementMain)
        let system = AudioObjectID(kAudioObjectSystemObject)

        var size: UInt32 = 0
        guard AudioObjectGetPropertyDataSize(system, &address, 0, nil, &size) == noErr, size > 0 else { return false }
        var devices = [AudioObjectID](repeating: 0, count: Int(size) / MemoryLayout<AudioObjectID>.size)
        guard AudioObjectGetPropertyData(system, &address, 0, nil, &size, &devices) == noErr else { return false }

        // Output devices also report "running" while audio plays, so only check devices with input streams.
        return devices.contains { device in
            var streams = AudioObjectPropertyAddress(
                mSelector: kAudioDevicePropertyStreams,
                mScope: kAudioDevicePropertyScopeInput,
                mElement: kAudioObjectPropertyElementMain)
            var streamSize: UInt32 = 0
            guard AudioObjectGetPropertyDataSize(device, &streams, 0, nil, &streamSize) == noErr,
                  streamSize > 0 else { return false }

            var runningAddress = AudioObjectPropertyAddress(
                mSelector: kAudioDevicePropertyDeviceIsRunningSomewhere,
                mScope: kAudioObjectPropertyScopeGlobal,
                mElement: kAudioObjectPropertyElementMain)
            var running: UInt32 = 0
            var runningSize = UInt32(MemoryLayout<UInt32>.size)
            return AudioObjectGetPropertyData(device, &runningAddress, 0, nil, &runningSize, &running) == noErr
                && running != 0
        }
    }
}
