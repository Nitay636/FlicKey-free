import AppKit

// Settings → About: the app version, the "free" note, and a replay for the
// welcome tour.
final class AboutSettingsViewController: NSViewController {

    override func loadView() {
        let header = sectionHeader("About")

        let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? ""
        let status = NSTextField(wrappingLabelWithString: "FlicKey \(version)")
        status.font = .systemFont(ofSize: 13)
        status.setAccessibilityIdentifier("aboutVersion")

        let note = NSTextField(wrappingLabelWithString:
            "FlicKey is free. Every feature is available, with no trial, license key, or account.")
        note.font = .systemFont(ofSize: 12)
        note.textColor = .secondaryLabelColor

        let tourButton = NSButton(title: "Show the Welcome Tour",
                                  target: self, action: #selector(showTour))
        tourButton.bezelStyle = .rounded

        let stack = NSStackView(views: [header, status, note, spacer(8), tourButton])
        installSettingsPane(stack, cards: [], spacing: 10, fixedHeight: 200)
    }

    @objc private func showTour() {
        WelcomeTourController.shared.present()
    }
}
