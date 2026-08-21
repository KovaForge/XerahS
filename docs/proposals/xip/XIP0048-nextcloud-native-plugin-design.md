# XIP0048 Nextcloud Native Plugin Design

**Status**: Complete
**Version**: v0.22.257

XIP0048: Nextcloud Native Plugin Design

## Goal
Replace the legacy `ownCloud / Nextcloud` compatibility model from classic ShareX with a dedicated XerahS plugin that follows current Nextcloud APIs and UX expectations.

## Legacy ShareX Baseline
Classic ShareX already has Nextcloud support, but it lives inside the older shared uploader path:

- `C:\Users\liveu\source\repos\ShareX Team\ShareX\ShareX.UploadersLib\FileUploaders\OwnCloud.cs`
- Transport: direct `PUT` to `remote.php/webdav`
- Sharing: OCS share creation with public-link mode
- Auth: manual username + password only
- Gaps: no Login Flow v2, no capability probing, no chunked upload v2, no explorer workflow, and several URL behaviors are ownCloud-era compatibility hacks rather than documented Nextcloud-native flows

## Official Nextcloud APIs Used For This Design
The plugin design and implementation are based on official Nextcloud documentation and endpoints:

1. Login Flow v2
   - `POST /index.php/login/v2`
   - Poll endpoint returns `server`, `loginName`, and `appPassword`
   - Source: `https://docs.nextcloud.com/server/latest/developer_manual/client_apis/LoginFlow/index.html`
2. User metadata
   - `GET /ocs/v1.php/cloud/user?format=json`
   - Used to resolve the real user ID for DAV paths because login name and DAV user ID can differ
   - Source: `https://docs.nextcloud.com/server/stable/developer_manual/client_apis/OCS/ocs-api-overview.html`
3. WebDAV file APIs
   - `remote.php/dav/files/<userId>/...`
   - `X-NC-WebDAV-AutoMkcol: 1` for nested path creation
   - Source: `https://docs.nextcloud.com/server/latest/developer_manual/client_apis/WebDAV/basic.html`
4. Chunked upload v2
   - `remote.php/dav/uploads/<userId>/<uploadId>/...`
   - finalize with `MOVE .../.file` and `OC-Total-Length`
   - Source: `https://docs.nextcloud.com/server/latest/developer_manual/client_apis/WebDAV/chunking.html`
5. OCS Share API
   - `POST /ocs/v2.php/apps/files_sharing/api/v1/shares?format=json`
   - public links, optional password, optional expire date
   - Source: `https://docs.nextcloud.com/server/latest/developer_manual/client_apis/OCS/ocs-share-api.html`
6. Capabilities
   - `GET /ocs/v2.php/cloud/capabilities?format=json`
   - used to detect public sharing, share password/expiry policy, DAV chunking, and server theming
   - Source: `https://docs.nextcloud.com/server/latest/developer_manual/client_apis/OCS/ocs-api-overview.html`

## Implemented In This XIP Pass
The first dedicated plugin pass is implemented under:

- `src/desktop/plugins/Nextcloud.Plugin/`

Implemented now:

1. Dedicated `NextcloudProvider` plugin for `Image`, `Text`, and `File`.
2. Dedicated `NextcloudConfigView` and `NextcloudConfigViewModel` instead of a generic property grid.
3. Browser-based Login Flow v2 start + finish workflow.
4. Secure storage for returned app password and optional share password.
5. Capability/profile refresh using OCS user + capabilities endpoints.
6. Native WebDAV upload path rooted at `remote.php/dav/files/<userId>/...`.
7. Chunked upload v2 support for larger files.
8. OCS public share creation with optional expiry and password.
9. Basic explorer support using WebDAV `PROPFIND`, `GET`, `DELETE`, and `MKCOL`.

## UX Direction
The UI intentionally follows the richer setup style of the Amazon S3 plugin rather than a plain form:

1. A top-level status card explains the destination, connection state, and detected capabilities.
2. Connection is staged:
   - server URL
   - browser login
   - profile refresh
3. Server profile is visible and read-only after verification:
   - display name
   - user ID
   - product/version/theming
4. Upload behavior and share behavior are separate sections instead of one mixed block.
5. Share policy is progressive disclosure:
   - create public share
   - expiry
   - password

## Why This Is Better Than The Legacy ShareX Uploader
1. It uses Nextcloud's documented browser login flow instead of forcing manual password entry.
2. It stores app passwords in the secret store instead of leaving them in plain JSON config.
3. It uploads against the current DAV root rather than the old `remote.php/webdav` path.
4. It probes server capabilities before assuming public-link, password, expiry, or chunking support.
5. It supports explorer scenarios directly from the provider instead of only upload-and-return.
6. It removes the old ownCloud compatibility URL shaping from the primary flow.

## Staged Follow-Up Work
The plugin now has a good native baseline, but there are clear follow-ups:

1. Add server-side search integration using Nextcloud's DAV search support instead of client-side filtering only.
2. Add share editing and re-use:
   - list existing shares for a path
   - update expiry/password instead of always creating a new link
3. Add richer preview support via Nextcloud preview endpoints for image thumbnails.
4. Add legacy config migration from ShareX `OwnCloud*` settings into a first-run Nextcloud plugin instance.
5. Add admin-policy messaging when the server enforces share password or expiry.

## Verification
1. `dotnet build src\desktop\plugins\Nextcloud.Plugin\XerahS.Nextcloud.Plugin.csproj -m:1`
2. `dotnet build src\desktop\XerahS.sln -m:1`

Both builds passed with `0` errors and `0` warnings in this implementation pass.