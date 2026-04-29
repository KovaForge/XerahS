# XerahS iOS App Store Submission

Validated on 2026-04-29 for bundle ID `com.getsharex.xerahs.mobile`.

## Repository-Ready Items

- Release simulator build passes with `CODE_SIGNING_ALLOWED=NO`.
- Unsigned generic iOS device build passes with `CODE_SIGNING_ALLOWED=NO`.
- App and share extension include privacy manifests for App Group `UserDefaults` access using required reason `1C8F.1`.
- App `Info.plist` declares `ITSAppUsesNonExemptEncryption` as `false`.
- App and share extension use marketing version `0.22.133` and build number `2`.
- Uploader secrets are migrated out of the JSON settings file and into Keychain.
- App Store Connect export options are in `ExportOptions.plist`.

## App Store Connect Checklist

- Agreements, Tax, and Banking: confirm the Apple Developer account has no pending agreement, tax, or banking actions.
- App Information: set category to Utilities, set Support URL, Marketing URL if available, and a public Privacy Policy URL.
- Privacy: answer App Privacy questions based on the current app behavior. The app does not use tracking, analytics SDKs, ads, camera, microphone, location, contacts, or photo-library permission prompts. It sends user-selected files only to destinations configured by the user, such as S3 or a custom uploader.
- Age Rating: complete the App Store Connect questionnaire. The expected rating should be low because the app has no user-generated content feed, gambling, commerce, social networking, or mature built-in content.
- Pricing and Availability: choose storefront availability and price tier.
- Screenshots: upload required iPhone screenshots, plus iPad screenshots because the app targets both device families.
- TestFlight: upload a signed archive, run at least one install test from TestFlight, and verify share-extension upload from Photos and Files.
- App Review: include notes that XerahS is a share-extension upload utility and that reviewers can test with a temporary S3 bucket or a simple HTTPS endpoint. Provide test credentials only through App Store Connect review notes.

## Archive and Upload

Run these commands from this directory after confirming the Apple account has signing access for team `5299HQVVA5`.

```sh
xcodebuild archive \
  -project XerahSMobile.xcodeproj \
  -scheme XerahSMobile \
  -configuration Release \
  -destination 'generic/platform=iOS' \
  -archivePath ./build/XerahSMobile.xcarchive

xcodebuild -exportArchive \
  -archivePath ./build/XerahSMobile.xcarchive \
  -exportPath ./build/AppStore \
  -exportOptionsPlist ExportOptions.plist
```

After export upload completes, finish TestFlight processing in App Store Connect before submitting for review.
