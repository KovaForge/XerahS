# Google Play Preview Asset Guidelines

Source: Google Play Console Help, "Add preview assets to showcase your app"
Last reviewed: 2026-05-02

This document summarizes the Play Store preview asset requirements and recommendations that apply to the XerahS Android listing. Treat Google Play Console Help and Google Play policy as the source of truth before final submission.

## Where Assets Are Managed

Manage preview assets in Play Console under:

`Grow users > Store presence > Main store listing > Graphics`

After assets are added to the store listing, they can appear across all test tracks. By publishing on Google Play, the app grants Google permission to use listing assets such as icons, screenshots, and videos for Play Store and promotional surfaces according to the Developer Distribution Agreement.

The "External marketing" setting can be unchecked under:

`Grow users > Store presence > Store settings`

Use that setting if XerahS should not be promoted across Google-owned properties outside the Play Store listing.

## Requirements vs Recommendations

Google separates guidance into two levels:

- Requirements: mandatory. Failing these can block publishing or lead to removal or suspension.
- Highly recommended: not always required for the listing page itself, but can affect how assets appear and whether the app is eligible for recommendation or promotion across Google Play.

All preview assets must comply with Google Play Developer Program Policies and the Metadata policy.

## App Icon

Required to publish the store listing.

Requirements:

- Format: 32-bit PNG with alpha.
- Size: 512 x 512 px.
- Maximum file size: 1024 KB.
- Must follow Google Play icon design specifications.
- Do not include badges or text that imply ranking, price, Google Play category, or anything misleading.

XerahS guidance:

- Use a high-resolution Play listing icon derived from the app identity, not a screenshot.
- Do not add text such as "Free", "Top", "New", "No ads", "Productivity", or "Android".

## Short Description

Required to publish the store listing.

Requirements:

- Maximum length: 80 characters.

Recommended:

- Explain the core purpose in simple language.
- Keep it current and avoid copy that becomes stale.
- Avoid duplication with screenshot text, feature graphic text, or video text.
- Avoid slang or jargon unless it is natural for the target audience.
- Do not include ranking, award, testimonial, price, promotion, or performance claims.
- Do not include calls to action such as "download now" or "try now".
- Do not keyword-stuff.
- Avoid special characters, line breaks, emojis, repeated punctuation, and capitalization for emphasis.
- Localize it for markets where XerahS has a localized listing.

XerahS guidance:

- Prefer: `Upload shared files to S3 or custom destinations from Android.`
- Avoid claims such as "best uploader", "free uploader", "top ShareX app", or "download now".

## Feature Graphic

Required to publish the store listing.

Requirements:

- Format: JPEG or 24-bit PNG without alpha.
- Size: 1024 x 500 px.

Recommended:

- Show the app experience or core value clearly.
- Keep focal content toward the center to avoid cropping on different Play surfaces.
- Avoid fine details that will not be visible on phones.
- Avoid pure white, pure black, and dark gray backgrounds that can blend into Play Store UI.
- Keep style and colors compatible with the app icon and app UI.
- Avoid heavy logo repetition, especially if it duplicates the app icon.
- Avoid device imagery because it can become obsolete.
- Avoid third-party trademarks unless permission is clear.
- Avoid Google Play badges, store icons, ranking claims, pricing claims, awards, testimonials, and time-sensitive content.
- Add alt text of 140 characters or less that identifies the important content without starting with "image of" or "photo of".

XerahS guidance:

- Use a clean visual that shows XerahS upload workflow context, such as a shared file moving into configured destinations.
- Do not put important UI, the app name, or any tagline close to the left or right edges.
- If text is used, keep it sparse and localizable.

## Phone Screenshots

At least two screenshots are required across supported device types to publish the listing. Up to eight screenshots can be added for each device type.

Requirements:

- Format: JPEG or 24-bit PNG without alpha.
- Minimum dimension: 320 px.
- Maximum dimension: 3840 px.
- The maximum dimension cannot be more than twice the minimum dimension.

Recommended for app promotion eligibility:

- Provide at least four screenshots at minimum 1080 px resolution.
- Use 9:16 portrait screenshots at minimum 1080 x 1920 px, or 16:9 landscape screenshots at minimum 1920 x 1080 px.
- Show the actual app experience and core features.
- Prioritize actual UI in the first three screenshots.
- Use taglines only when they clarify the feature, and keep tagline text under 20% of the image.
- Do not include ranking, award, testimonial, price, promotion, or Google Play performance claims.
- Do not include calls to action.
- Avoid small text, clutter, blurry images, distortion, stretching, compression, wrong rotation, or screenshots split in a way that hides the app UI.
- Clean the notification/status bar before submitting. Do not show notifications or carrier/service provider details.
- Avoid device frames, third-party trademarks, Google Play badges, and repetitive decorative elements.
- Add alt text of 140 characters or less for each screenshot.

XerahS recommended screenshot set:

1. Home screen with recent upload history.
2. Settings screen showing destination configuration.
3. S3 setup screen with secret fields masked.
4. Custom uploader import confirmation.
5. First-upload confirmation.
6. Upload complete result with generated URL.
7. History delete or clear controls.

## Large Screen Screenshots

For tablets and Chromebooks, Google recommends at least four screenshots that demonstrate the in-app experience.

Requirements and recommendations:

- Upload screenshots between 1080 and 7680 px.
- Use 16:9 for landscape and 9:16 for portrait.
- Avoid extra text that is not part of the app experience because it may be cropped on some Play surfaces.
- Review Android large-screen quality guidance before opting into those device classes.

XerahS guidance:

- Only provide tablet or Chromebook screenshots if the production build has been validated on those form factors.
- Do not imply tablet-specific optimization unless the UI has been checked on tablet layouts.

## Preview Video

A preview video is optional for apps. Add it by entering a YouTube URL in the Play Console preview video field.

Requirements:

- Use a YouTube video URL, not a playlist or channel URL.
- Do not include extra URL parameters such as timecodes.
- The video must be public or unlisted.
- The video must not be private or age-restricted.
- The video must be embeddable on Google Play.
- Ads must be disabled. If copyright monetization claims still cause ads, use a different video.

Recommended:

- Show actual in-app experience as early as possible, ideally within the first 10 seconds.
- Aim for at least 80% of the video to represent real user experience.
- Keep title screens, logos, and promotional content brief.
- Keep the video short; only the first 30 seconds may autoplay.
- Use portrait or landscape based on the app experience.
- Avoid black bars.
- Use captions.
- If text overlays are needed for muted autoplay, make them readable and avoid calls to action, pricing claims, ranking claims, awards, testimonials, and stale time-sensitive copy.
- Localize the video where appropriate.

XerahS guidance:

- A preview video is not required for the first internal testing upload.
- If one is created later, record the actual Android app flow: share file, confirm upload, copy URL, review history.

## Android TV, Wear OS, Automotive, and XR

These sections only apply if XerahS opts into those device categories.

Do not provide TV, Wear OS, Automotive, or XR assets unless the Android app actually supports those experiences and the production build has been tested there.

Notable requirements if support is added later:

- Android TV requires at least one TV screenshot and a TV banner image.
- TV banner format is JPEG or 24-bit PNG without alpha, 1280 x 720 px.
- Wear OS screenshots must show only the app or watch face interface, without device frames or extra backgrounds.
- Android XR requires 4 to 8 screenshots, PNG or JPEG up to 8 MB each, with an 8:5 aspect ratio.

## XerahS Pre-Submission Asset Checklist

- App icon is 512 x 512 px, 32-bit PNG with alpha, under 1024 KB.
- Feature graphic is 1024 x 500 px, JPEG or 24-bit PNG without alpha.
- At least four phone screenshots are prepared at 1080 x 1920 px portrait where practical.
- Screenshots show the current Android UI, not desktop ShareX-only features.
- Screenshots avoid device frames, Play badges, ranking claims, pricing claims, and calls to action.
- Status bars are clean and do not show private notifications.
- Sensitive values are masked in screenshots.
- Generated upload URLs in screenshots are test URLs, not private production URLs.
- Every uploaded graphic and screenshot has useful alt text under 140 characters.
- Listing copy and screenshot text match the app's actual behavior and current privacy policy.
