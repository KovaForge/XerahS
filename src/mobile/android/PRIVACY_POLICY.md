# Privacy Policy

Effective date: 2026-05-02

This Privacy Policy applies to XerahS Android, published by ShareX Team.

## Contact

Privacy contact: support@getsharex.com

## Data XerahS Android Accesses

XerahS Android can access files, images, videos, audio, text, URLs, and `.sxcu` or `.xsdc` configuration files that you select in the app or share to the app through Android. The app also stores upload configuration that you enter, including S3-compatible storage settings and custom uploader settings.

XerahS Android does not request camera, microphone, location, contacts, SMS, phone, notification, or broad external storage permissions.

## Uploads And Third-Party Destinations

When you start an upload, XerahS Android transmits the selected content to the upload destination that you configure. Supported destinations can include S3-compatible storage and custom uploader endpoints. These destinations are controlled by you or by third-party providers, not by XerahS Android by default.

Custom uploaders can send files, text, headers, parameters, request bodies, and authentication values to the endpoint configured in the uploader. XerahS Android rejects HTTP upload endpoints and requires HTTPS for custom uploader requests.

XerahS Android does not sell user data.

## Local Storage

XerahS Android stores settings, encrypted sensitive upload configuration, upload history, and temporary shared files in app-private Android storage. Settings and history are excluded from Android backup and device transfer. Temporary shared files are removed after upload processing where possible and stale cache files are cleaned up automatically.

## Security

Sensitive S3 and custom uploader values are encrypted with Android Keystore-backed storage before being written to app settings. Network upload and remote import flows are HTTPS-only for successful supported configurations.

## Retention And Deletion

Settings remain on the device until you change them, reset settings, or uninstall the app. Upload history remains on the device until you delete individual history items, clear history, reset settings, or uninstall the app. Temporary shared files are retained only temporarily in cache.

Deleting local settings or history in XerahS Android does not delete files already uploaded to a third-party destination. Deletion from those services depends on the provider and on your destination configuration.

## Clipboard

XerahS Android can read clipboard content only when you use an explicit import action, such as importing `.sxcu` JSON from the clipboard. The app can copy uploaded URLs to the clipboard after upload actions so you can paste the result.

## Canonical URL

The canonical public URL for this policy is `https://xerahs.com/privacy.html`.
