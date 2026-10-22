import XCTest

// UI tests for the Settings → About tab: version line and the free-to-use note.
final class AboutUITests: XCTestCase {

    private var app: XCUIApplication!

    override func setUpWithError() throws {
        continueAfterFailure = false
        forceLatinInputSource()
        app = XCUIApplication()
        app.launchArguments = ["-uiTestOpenSettings", "-uiTestReset",
                               "-uiTestSettingsTab", "About"]
        app.launch()
    }

    override func tearDownWithError() throws {
        app.terminate()
    }

    func testAboutTabShowsVersionAndFreeNote() {
        step("About tab — expect the version line and the free note, no purchase controls")
        XCTAssertTrue(app.windows["About"].waitForExistence(timeout: 10),
                      "Settings should open directly on About")
        let version = app.descendants(matching: .any).matching(identifier: "aboutVersion").firstMatch
        XCTAssertTrue(version.waitForExistence(timeout: 10))
        XCTAssertTrue((version.value as? String)?.hasPrefix("FlicKey ") == true)
        XCTAssertTrue(app.buttons["Show the Welcome Tour"].exists)
        XCTAssertFalse(app.buttons["buyLicense"].exists, "there is nothing to buy")
        XCTAssertFalse(app.buttons["activateLicense"].exists, "there is no license to activate")
    }
}
