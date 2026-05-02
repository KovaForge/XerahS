# XerahS Android Play Store Submission Checklist

Last reviewed: 2026-05-02.

## Build Artifact

- [x] Target Android 15 / API 35 or higher for Google Play submissions.
- [x] Build with Android Gradle Plugin 8.6.1 and Gradle 8.7, which support API 35.
- [x] Align Android `versionName` with the root XerahS version, currently `0.22.170`.
- [x] Produce a release Android App Bundle with `./gradlew :app:bundleRelease`.
- [x] Enable release R8 minification and resource shrinking.
- [x] Configure release signing from environment variables when an upload key is available.
- [ ] Sign the release bundle with the Play upload key before upload.
- [ ] Enroll the app in Play App Signing or use the existing Play App Signing setup.
- [ ] Keep `versionCode` monotonically increasing for every Play upload.

## App Content

- [x] Integrate the XerahS Android privacy policy into the public website privacy policy.
- [x] Add the Android privacy policy URL in the app About screen.
- [ ] Publish the public privacy policy URL for XerahS Android at `https://xerahs.com/privacy-policy/`.
- [ ] Add the same privacy policy URL in Play Console.
- [ ] Complete the Play Console Data safety form.
- [ ] Declare that user-selected files can be transmitted to user-configured upload destinations.
- [ ] Declare that upload configuration may include authentication data such as S3 keys or custom HTTP headers.
- [ ] Declare that uploaded URLs, file names, and local history metadata are stored on device.
- [ ] Confirm no account creation is offered by the Android app; if that changes, add account deletion flow and URL.
- [ ] Confirm no ads SDK or Advertising ID permission is included.
- [ ] Confirm no high-risk permissions are requested. Current manifest only requests `INTERNET`.

## Privacy And Security

- [x] Exclude settings and history files from Android backup and device transfer.
- [x] Encrypt sensitive S3 and custom uploader values with Android Keystore-backed storage.
- [x] Reject HTTP custom uploader endpoints and HTTP remote `.sxcu` import URLs.
- [x] Require remote `.sxcu` import confirmation with source, uploader, request URL, method, and data-shape warnings.
- [x] Apply timeout, redirect, content-type, and 1 MB size limits to remote `.sxcu` imports.
- [x] Redact sensitive custom uploader errors before showing or copying details.
- [x] Clean copied shared files after upload processing where possible and clean stale cache files on startup.

## Store Listing

- [ ] App name: XerahS.
- [ ] Short description, full description, category, contact email, website, and privacy policy URL.
- [ ] High-resolution 512 x 512 Play icon.
- [ ] Feature graphic if required for the target track/listing.
- [ ] Phone screenshots showing Settings, share upload flow, progress/results, and History.
- [ ] Content rating questionnaire.
- [ ] Target audience and app access declarations.
- [x] Prepare listing, Data Safety, reviewer instruction, and screenshot guidance in `PLAY_STORE_LISTING.md`.

## Validation Before Upload

- [x] `JAVA_HOME=/usr/local/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home ANDROID_HOME=/usr/local/share/android-commandlinetools ./gradlew assembleDebug`
- [x] `JAVA_HOME=/usr/local/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home ANDROID_HOME=/usr/local/share/android-commandlinetools ./gradlew :app:bundleRelease`
- [ ] `JAVA_HOME=/usr/local/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home ANDROID_HOME=/usr/local/share/android-commandlinetools ./gradlew clean :app:lintRelease :app:bundleRelease`
- [ ] Install and smoke-test the debug APK on a physical device or emulator.
- [ ] Test Android share sheet flows for image, video, text, PDF, and multiple files.
- [ ] Test S3 upload success and failure states.
- [ ] Test custom uploader success and failure states.
- [ ] Test History copy/delete/clear behavior.
- [ ] Check release bundle in Play Console pre-launch report.

## Release Signing

Use Play App Signing and keep the upload key outside the repository. Gradle reads the upload signing configuration from environment variables and leaves release bundles unsigned when these values are not present.

Suggested local release signing inputs:

- `XERAHS_ANDROID_UPLOAD_STORE_FILE`
- `XERAHS_ANDROID_UPLOAD_STORE_PASSWORD`
- `XERAHS_ANDROID_UPLOAD_KEY_ALIAS`
- `XERAHS_ANDROID_UPLOAD_KEY_PASSWORD`

Do not commit keystores, signing passwords, or generated Play Console service credentials.

## References

- Google Play target API level requirement: https://developer.android.com/google/play/requirements/target-sdk
- Android App Bundle publishing format: https://developer.android.com/guide/app-bundle/
- Android release preparation and signing: https://developer.android.com/studio/publish/preparing
- Google Play User Data policy: https://support.google.com/googleplay/android-developer/answer/10144311
- Google Play Data safety form guidance: https://support.google.com/googleplay/android-developer/answer/10787469
