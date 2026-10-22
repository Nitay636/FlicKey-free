import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {

    private var statusBar: StatusBarController?
    private let hotkeyManager = HotkeyManager()
    private let appWatcher = AppWatcher()
    private let focusWatcher = FocusWatcher()
    private let browserCatalogObserver = BrowserCatalogObserver()
    private let tabMemory = TabMemory()
    private let conversationMemory = AppConversationMemory()
    private let layoutCue = LayoutCueController()
    private let autoSwitch = AutoSwitchController()
    private let accessibilityMonitor = AccessibilityTrustMonitor()
    private var coreStarted = false
    private var accessibilityFeaturesRunning = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        // UI tests: redirect all settings to a throwaway store BEFORE anything
        // reads it, so tests can't read or corrupt the user's real settings.
        if UITestMode.isActive {
            AppDefaults.useIsolatedStoreForUITests(reset: UITestMode.shouldResetState)
        }
        // Apply the login-item default (and show the welcome tour) only on a
        // genuinely fresh install. Read this before writing any preference.
        let isFreshInstall = !PriorUseEvidence.exists(in: AppDefaults.store)
        // UI tests: seed per-site memory into the (isolated) store. Value form:
        //   "domain=sourceID;domain=sourceID"
        let args = ProcessInfo.processInfo.arguments
        if UITestMode.isActive && args.contains("-uiTestDisableRememberVisitedApps") {
            // App-routing fixtures need genuinely unlisted apps. The normal
            // default learns a rule on first visit, which invalidates their
            // "no rule" premise even in an otherwise reset test store.
            RulesStore.setRememberVisitedAppsEnabled(false)
        }
        if UITestMode.isActive, let i = args.firstIndex(of: "-uiTestSeedSites"), i + 1 < args.count {
            for pair in args[i + 1].split(separator: ";") {
                let kv = pair.split(separator: "=", maxSplits: 1)
                if kv.count == 2 { SiteMemoryStore.set(String(kv[1]), for: String(kv[0])) }
            }
        }
        // UI tests: seed per-app forced rules into the (isolated) store. Value
        // form: "AppName=sourceID;AppName=sourceID". Each app is added as a
        // custom entry (rules only resolve for listed apps) with an override.
        if UITestMode.isActive, let i = args.firstIndex(of: "-uiTestSeedAppRules"), i + 1 < args.count {
            for pair in args[i + 1].split(separator: ";") {
                let kv = pair.split(separator: "=", maxSplits: 1)
                guard kv.count == 2 else { continue }
                let name = String(kv[0])
                RulesStore.addCustomApp(CustomApp(name: name, bundleID: ""))
                RulesStore.set(String(kv[1]), forMatchKey: name.lowercased())
            }
        }
        if UITestMode.isActive && args.contains("-uiTestEnableAutoSwitch") {
            AutoSwitchSettings.isEnabled = true
        }

        statusBar = StatusBarController(tabMemory: tabMemory)
        browserCatalogObserver.start()
        accessibilityMonitor.onChange = { [weak self] trusted in
            self?.accessibilityTrustChanged(trusted)
        }
        accessibilityMonitor.onCheck = { [weak self] trusted in
            self?.accessibilityTrustChecked(trusted)
        }
        accessibilityMonitor.start()
        requestAccessibilityPermission()

        // After a conversion, switch the keyboard layout to the converted
        // language so the user's next keystrokes are in the intended language.
        // The switch is FlicKey's own doing and the conversion already clicked,
        // so the layout-switch cue is muted for it.
        hotkeyManager.onConversion = { [weak self] targetSourceID in
            self?.layoutCue.suppress()
            InputSourceManager.switchTo(sourceID: targetSourceID)
        }
        // A ⇧⇧ right after an auto-switch undoes it instead of converting again.
        hotkeyManager.onUndoTrigger = { [weak self] in
            _ = self?.autoSwitch.handleUndoTrigger()
        }

        // Haptic/click cue on every effective layout change (app, tab, or
        // conversation switch — and manual ⌘Space). Cosmetic.
        layoutCue.start()

        // Auto-correct (opt-in, default off): when two consecutive words are
        // confidently wrong-layout, fix them and switch the input source. Its own
        // switch mutes the layout cue, like a manual conversion.
        autoSwitch.suppressCue = { [weak self] in self?.layoutCue.suppress() }
        NotificationCenter.default.addObserver(
            forName: .autoSwitchSettingChanged, object: nil, queue: .main) { [weak self] _ in
            guard let self, self.coreStarted else { return }
            if AutoSwitchSettings.isEnabled {
                if self.accessibilityMonitor.isTrusted { self.autoSwitch.start() }
                else { AutoSwitchMonitorHealth.set(.unavailable(.accessibilityDenied)) }
            } else {
                self.autoSwitch.stop()
            }
        }

        // Per-app automatic input switching. Browsers (AUTO) are handed to
        // TabMemory (per-website memory); apps with a conversation provider
        // (e.g. Teams) are handed to AppConversationMemory (per-conversation
        // memory). The two controllers are mutually exclusive: each activation
        // path releases the other so only one is ever tracking. (Started at
        // the end of launch.)
        appWatcher.onBrowserActivated = { [weak self] app in
            self?.conversationMemory.leftApp()
            self?.tabMemory.browserActivated(app)
        }
        appWatcher.onConversationApp = { [weak self] app in
            self?.tabMemory.leftBrowser()
            self?.conversationMemory.appActivated(app)
        }
        appWatcher.onLeftBrowser = { [weak self] in
            self?.conversationMemory.leftApp()
            self?.tabMemory.leftBrowser()
        }

        // Non-activating panels (e.g. Ghostty's quick terminal) grab the
        // keyboard WITHOUT posting an app activation, so AppWatcher never sees
        // them and the forced layout isn't applied (nor restored on dismiss).
        // FocusWatcher watches the focused element of every forced-source app and
        // routes both the panel summon and its dismiss through the very same
        // AppWatcher router (so controller-release and switching stay identical to
        // a normal activation).
        focusWatcher.applyRule = { [weak self] app in
            self?.appWatcher.handle(app)
        }

        startCoreFeatures()

        if !UITestMode.isActive, isFreshInstall {
            _ = LaunchAtLogin.setEnabled(true)
        }

        // Marks that this install has completed a launch, so a later launch
        // (or a reinstall that keeps preferences) is not treated as fresh.
        if !UITestMode.isActive { AppDefaults.store.set(true, forKey: "hasCompletedFirstLaunch") }

        // One-time / periodic launch surfaces, strictly one per launch.
        // Precedence: the welcome tour (fresh installs; their version IS the
        // baseline, so it also consumes the what's new note), then a what's new
        // note for updating users, then the blocked-words share offer.
        if WelcomeTourController.showIfNeeded(isFreshInstall: isFreshInstall) {
            if let note = WhatsNew.current { WhatsNewStore.seenVersion = note.version }
        } else if !WhatsNewController.showIfNeeded() {
            BlockedWordsSharingController.runIfNeeded()
        }

        // UI-test hook: open Settings on launch so the XCUITest suite doesn't
        // depend on clicking the (flaky to automate) menu-bar status item.
        if UITestMode.isActive && args.contains("-uiTestOpenSettings") {
            statusBar?.presentSettingsForTesting()
        }
    }

    // The input-switching features. Idempotent via coreStarted.
    private func startCoreFeatures() {
        guard !coreStarted else { return }
        coreStarted = true
        SoundEffect.warm()   // preload the click so the first fix's sound is instant
        tabMemory.start()
        conversationMemory.start()
        appWatcher.start()
        refreshAccessibilityFeatures()
    }

    private func accessibilityTrustChanged(_ trusted: Bool) {
        guard coreStarted else { return }
        refreshAccessibilityFeatures()
    }

    private func accessibilityTrustChecked(_ trusted: Bool) {
        guard coreStarted, trusted, AutoSwitchSettings.isEnabled,
              case .unavailable(.monitorCreationFailed) = AutoSwitchMonitorHealth.state
        else { return }
        autoSwitch.retryTypingMonitor()
    }

    private func refreshAccessibilityFeatures() {
        guard coreStarted else { return }
        if accessibilityMonitor.isTrusted {
            if !accessibilityFeaturesRunning {
                hotkeyManager.start()
                focusWatcher.start()
                accessibilityFeaturesRunning = true
            }
            if AutoSwitchSettings.isEnabled { autoSwitch.start() }
        } else {
            if accessibilityFeaturesRunning {
                hotkeyManager.stop()
                focusWatcher.stop()
                accessibilityFeaturesRunning = false
            }
            autoSwitch.stop()
            if AutoSwitchSettings.isEnabled {
                AutoSwitchMonitorHealth.set(.unavailable(.accessibilityDenied))
            }
        }
    }

    // Phase 1: prompt for Accessibility access. Real hotkey/AX usage comes later.
    private func requestAccessibilityPermission() {
        let trusted = AccessibilityAccess.requestIfNeeded()

        if !trusted {
            let alert = NSAlert()
            alert.messageText = "Accessibility Access Required"
            alert.informativeText = """
            FlicKey needs Accessibility access to read window titles \
            and send keystrokes.

            Open System Settings › Privacy & Security › Accessibility and enable \
            FlicKey.
            """
            alert.alertStyle = .warning
            alert.addButton(withTitle: "Open System Settings")
            alert.addButton(withTitle: "Later")

            if alert.runModal() == .alertFirstButtonReturn {
                AccessibilityAccess.openSettings()
            }
        }
    }
}
