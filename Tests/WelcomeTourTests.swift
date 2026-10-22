import XCTest

// The welcome tour gate: fresh installs only, once.
final class WelcomeTourTests: XCTestCase {
    func testFreshInstallShows() {
        XCTAssertTrue(WelcomeTour.shouldShow(isFreshInstall: true, seen: false))
    }

    func testExistingUserNeverSees() {
        XCTAssertFalse(WelcomeTour.shouldShow(isFreshInstall: false, seen: false))
    }

    func testSeenNeverRepeats() {
        XCTAssertFalse(WelcomeTour.shouldShow(isFreshInstall: true, seen: true))
    }
}
