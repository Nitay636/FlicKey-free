import Foundation

// The one-time welcome tour for NEW users: a small animated walkthrough of what
// FlicKey does, shown on the first launch after a fresh install. Existing users
// never see it (they get the What's New note instead); the About tab has a
// button to replay it anytime.
enum WelcomeTour {
    // Pure gate so it is unit-testable: show only for an unseen, genuinely fresh
    // install (no surviving preferences from an earlier launch).
    static func shouldShow(isFreshInstall: Bool, seen: Bool) -> Bool {
        !seen && isFreshInstall
    }
}

enum WelcomeTourStore {
    private static let key = "welcomeTourSeen"
    static var seen: Bool {
        get { AppDefaults.store.bool(forKey: key) }
        set { AppDefaults.store.set(newValue, forKey: key) }
    }
}
