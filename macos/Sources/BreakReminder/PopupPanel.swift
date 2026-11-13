import AppKit
import BreakReminderCore

enum PopupResult { case done, snooze, timedOut }

/// Button that responds to the first click even though its panel never becomes key.
private final class FirstClickButton: NSButton {
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
}

/// Small corner popup that never takes focus, shows on every Space, and opts out of screen capture.
final class PopupPanel: NSPanel {
    private static let width: CGFloat = 320
    private static let margin: CGFloat = 12

    let kind: ReminderKind
    var onFinish: ((PopupPanel, PopupResult) -> Void)?

    private let idle: () -> TimeInterval
    private let countdownStart: Int
    private var countdownLeft: Int
    private var autoHideLeft: Int
    private var countdownLabel: NSTextField?
    private var timer: Timer?
    private var finished = false

    init(kind: ReminderKind, title: String, body: String, countdownSeconds: Int, autoHideSeconds: Int,
         allowSnooze: Bool, accent: NSColor, idle: @escaping () -> TimeInterval) {
        self.kind = kind
        self.idle = idle
        countdownStart = countdownSeconds
        countdownLeft = countdownSeconds
        autoHideLeft = autoHideSeconds
        super.init(contentRect: NSRect(x: 0, y: 0, width: Self.width, height: 100),
                   styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)

        level = .statusBar
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        isFloatingPanel = true
        hidesOnDeactivate = false
        isReleasedWhenClosed = false
        // Best effort: macOS 15+ ScreenCaptureKit-based sharing may still capture the panel.
        sharingType = .none
        backgroundColor = .clear
        isOpaque = false
        hasShadow = true

        contentView = buildContent(title: title, body: body, allowSnooze: allowSnooze, accent: accent)
        setContentSize(contentView!.fittingSize)
    }

    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    /// Slides the popup into the bottom-right corner of the screen under the mouse.
    func showInCorner() {
        let mouse = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { NSMouseInRect(mouse, $0.frame, false) } ?? NSScreen.main
        guard let area = screen?.visibleFrame else { return }

        let target = NSRect(x: area.maxX - frame.width - Self.margin, y: area.minY + Self.margin,
                            width: frame.width, height: frame.height)
        setFrame(target.offsetBy(dx: 0, dy: -20), display: false)
        alphaValue = 0
        orderFrontRegardless()
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.2
            animator().setFrame(target, display: true)
            animator().alphaValue = 1
        }

        let timer = Timer(timeInterval: 1, repeats: true) { [weak self] _ in self?.onSecond() }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }

    /// Closes without calling `onFinish`.
    func closeSilently() {
        finished = true
        dismiss()
    }

    private func onSecond() {
        if let countdownLabel, countdownLeft > 0 {
            countdownLeft -= 1
            countdownLabel.stringValue = countdownText
            // No input during the countdown means the user looked away; count it as done.
            if countdownLeft == 0 && idle() >= TimeInterval(countdownStart - 2) {
                finish(.done)
                return
            }
        }
        autoHideLeft -= 1
        if autoHideLeft <= 0 { finish(.timedOut) }
    }

    private var countdownText: String { countdownLeft > 0 ? "\(countdownLeft) s" : "Time's up" }

    private func finish(_ result: PopupResult) {
        guard !finished else { return }
        finished = true
        onFinish?(self, result)
        dismiss()
    }

    private func dismiss() {
        timer?.invalidate()
        timer = nil
        orderOut(nil)
    }

    private func buildContent(title: String, body: String, allowSnooze: Bool, accent: NSColor) -> NSView {
        let background = NSVisualEffectView()
        background.material = .hudWindow
        background.blendingMode = .behindWindow
        background.state = .active
        background.wantsLayer = true
        background.layer?.cornerRadius = 10
        background.layer?.masksToBounds = true

        let accentBar = NSView()
        accentBar.wantsLayer = true
        accentBar.layer?.backgroundColor = accent.cgColor

        let titleLabel = NSTextField(labelWithString: title)
        titleLabel.font = .boldSystemFont(ofSize: NSFont.systemFontSize)

        let bodyLabel = NSTextField(wrappingLabelWithString: body)
        bodyLabel.font = .systemFont(ofSize: NSFont.systemFontSize)
        bodyLabel.preferredMaxLayoutWidth = Self.width - 4 - 28

        let buttons = NSStackView()
        buttons.orientation = .horizontal
        buttons.spacing = 8
        if countdownStart > 0 {
            let label = NSTextField(labelWithString: countdownText)
            label.textColor = .secondaryLabelColor
            countdownLabel = label
            buttons.addArrangedSubview(label)
        }
        let spacer = NSView()
        spacer.setContentHuggingPriority(.defaultLow, for: .horizontal)
        buttons.addArrangedSubview(spacer)
        if allowSnooze {
            buttons.addArrangedSubview(makeButton("Snooze 5 min", result: .snooze))
        }
        let done = makeButton("Done", result: .done)
        done.bezelColor = accent
        buttons.addArrangedSubview(done)

        let stack = NSStackView(views: [titleLabel, bodyLabel, buttons])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 6
        stack.setCustomSpacing(12, after: bodyLabel)

        for view in [accentBar, stack] {
            view.translatesAutoresizingMaskIntoConstraints = false
            background.addSubview(view)
        }
        NSLayoutConstraint.activate([
            background.widthAnchor.constraint(equalToConstant: Self.width),
            accentBar.leadingAnchor.constraint(equalTo: background.leadingAnchor),
            accentBar.topAnchor.constraint(equalTo: background.topAnchor),
            accentBar.bottomAnchor.constraint(equalTo: background.bottomAnchor),
            accentBar.widthAnchor.constraint(equalToConstant: 4),
            stack.leadingAnchor.constraint(equalTo: accentBar.trailingAnchor, constant: 14),
            stack.trailingAnchor.constraint(equalTo: background.trailingAnchor, constant: -14),
            stack.topAnchor.constraint(equalTo: background.topAnchor, constant: 12),
            stack.bottomAnchor.constraint(equalTo: background.bottomAnchor, constant: -12),
            buttons.widthAnchor.constraint(equalTo: stack.widthAnchor),
        ])
        return background
    }

    private func makeButton(_ title: String, result: PopupResult) -> NSButton {
        let button = FirstClickButton(title: title, target: nil, action: nil)
        button.bezelStyle = .rounded
        button.target = self
        button.action = result == .done ? #selector(doneClicked) : #selector(snoozeClicked)
        return button
    }

    @objc private func doneClicked() { finish(.done) }
    @objc private func snoozeClicked() { finish(.snooze) }
}
