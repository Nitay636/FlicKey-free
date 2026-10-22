## Status

0.7.0 (build 79): trial mode and licensing removed from the 0.6.0 source. There is no entitlement gate, license store, Lemon Squeezy client, trial clock, reminder/nag panels or Keychain use. The Settings "Support" tab is now "About". The Sparkle updater is removed (its feed would reinstall the original trial build). The release/notarization scripts and the signed-release trial tests were deleted.

## Notes

- The welcome tour now shows when no preferences from an earlier launch exist (previously a Keychain first-run stamp).
- Legacy preference keys (e.g. `statsNagDisabled`) stay in `PriorUseEvidence` so upgrades are still detected.
- Build from source with XcodeGen; `project.yml` pins the original author's team, so switch signing to your own.
