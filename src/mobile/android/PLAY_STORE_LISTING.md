# XerahS Android Play Store Listing Draft

## App Identity

App name: XerahS

Developer: ShareX Team

Category: Productivity or Tools

Ads declaration: No ads, while the current dependency set remains free of ad SDKs and the manifest does not request Advertising ID access.

Target audience: Not child-directed.

Privacy policy URL: `https://xerahs.com/privacy-policy/`

## Short Description

Upload files you select or share to your own configured destinations.

## Full Description

XerahS Android lets you upload files, media, text, and shared content from Android to destinations you configure, including S3-compatible storage and custom uploader endpoints.

The app is designed for user-initiated uploads. XerahS Android does not host uploaded files by default; files are sent to the destination you choose and configure. Custom uploaders can send files, text, request headers, parameters, bodies, and authentication values to third-party endpoints, so only import uploaders from sources you trust.

Current Android features include:

- Android share sheet upload support for images, videos, audio, text, JSON, and supported XerahS/ShareX configuration files.
- S3-compatible destination configuration.
- Custom `.sxcu` uploader import and editing.
- First-upload confirmation before files are sent to configured destinations.
- Local upload history with delete and clear controls.
- Encrypted local storage for sensitive upload configuration.

## Data Safety Draft

Use this as the Play Console starting point, then verify every answer against the exact production build uploaded to Play.

- Data collection: User-selected files and content may be transmitted off device when the user starts an upload.
- Data shared with third parties: User-selected content, upload metadata, and configured request data can be sent to S3-compatible storage, custom uploader endpoints, or other destinations configured by the user.
- Purpose: App functionality.
- User initiated: Yes, uploads are initiated by share/upload/import actions.
- Encrypted in transit: Yes for supported successful upload and remote import flows, because HTTP upload/import URLs are rejected and HTTPS is required.
- Optional or required: Treat upload data as core app functionality unless the final listing positions non-upload use as meaningful.
- Data deletion: Users can clear local history and reset local credentials/settings. Deletion from third-party upload destinations depends on that provider and the user's configuration.
- No sale of data: Do not mark user-initiated uploads as sale.

## Reviewer Instructions

1. Open XerahS.
2. Configure an S3-compatible test destination or import a trusted HTTPS `.sxcu` custom uploader.
3. Share an image, video, audio file, or text item to XerahS from Android's share sheet.
4. Confirm the first-upload warning.
5. Verify that XerahS shows the uploaded URL and copies the URL to the clipboard after a successful upload.
6. Open History to copy or delete uploaded entries.
7. Open Settings to clear history or reset credentials/settings.

If test credentials are not provided, reviewers can still verify app launch, settings screens, import validation, HTTPS-only rejection, first-upload disclosure, and local deletion controls.

## Screenshot Set

Prepare screenshots from the actual Android build:

- Settings hub with configured destination state.
- S3 configuration screen with secret field masked.
- Custom uploader import confirmation.
- First-upload confirmation dialog.
- Upload progress and completed URL result.
- History list with delete/clear controls.
