# XerahS Android Play Store Submission Checklist

Last reviewed: 2026-04-29.

## Build Artifact

- [x] Target Android 15 / API 35 or higher for Google Play submissions.
- [x] Build with Android Gradle Plugin 8.6.1 and Gradle 8.7, which support API 35.
- [x] Align Android `versionName` with the root XerahS version, currently `0.22.133`.
- [ ] Produce a release Android App Bundle with `./gradlew bundleRelease`.
- [ ] Sign the release bundle with the Play upload key before upload.
- [ ] Enroll the app in Play App Signing or use the existing Play App Signing setup.
- [ ] Keep `versionCode` monotonically increasing for every Play upload.

## App Content

- [ ] Create or update the public privacy policy URL for XerahS Android.
- [ ] Add the same privacy policy URL in Play Console and in the app or settings UI.
- [ ] Complete the Play Console Data safety form.
- [ ] Declare that user-selected files can be transmitted to user-configured upload destinations.
- [ ] Declare that upload configuration may include authentication data such as S3 keys or custom HTTP headers.
- [ ] Declare that uploaded URLs, file names, and local history metadata are stored on device.
- [ ] Confirm no account creation is offered by the Android app; if that changes, add account deletion flow and URL.
- [ ] Confirm no ads SDK or Advertising ID permission is included.
- [ ] Confirm no high-risk permissions are requested. Current manifest only requests `INTERNET`.

## Privacy And Security

- [x] Exclude settings and history files from Android backup and device transfer.
- [ ] Consider encrypting upload credentials with Android Keystore before first production release.
- [ ] Ensure custom uploader documentation recommends HTTPS endpoints for transmitted user files and headers.
- [ ] Verify cache cleanup behavior for copied shared files after successful and failed uploads.

## Store Listing

- [ ] App name: XerahS.
- [ ] Short description, full description, category, contact email, website, and privacy policy URL.
- [ ] High-resolution 512 x 512 Play icon.
- [ ] Feature graphic if required for the target track/listing.
- [ ] Phone screenshots showing Settings, share upload flow, progress/results, and History.
- [ ] Content rating questionnaire.
- [ ] Target audience and app access declarations.

## Validation Before Upload

- [ ] `JAVA_HOME=/usr/local/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home ANDROID_HOME=/usr/local/share/android-commandlinetools ./gradlew assembleDebug`
- [ ] `JAVA_HOME=/usr/local/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home ANDROID_HOME=/usr/local/share/android-commandlinetools ./gradlew bundleRelease`
- [ ] Install and smoke-test the debug APK on a physical device or emulator.
- [ ] Test Android share sheet flows for image, video, text, PDF, and multiple files.
- [ ] Test S3 upload success and failure states.
- [ ] Test custom uploader success and failure states.
- [ ] Test History copy/delete/clear behavior.
- [ ] Check release bundle in Play Console pre-launch report.

## References

- Google Play target API level requirement: https://developer.android.com/google/play/requirements/target-sdk
- Android App Bundle publishing format: https://developer.android.com/guide/app-bundle/
- Android release preparation and signing: https://developer.android.com/studio/publish/preparing
- Google Play User Data policy: https://support.google.com/googleplay/android-developer/answer/10144311
- Google Play Data safety form guidance: https://support.google.com/googleplay/android-developer/answer/10787469
