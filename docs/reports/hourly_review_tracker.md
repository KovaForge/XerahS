# XerahS Hourly Review Current Tracker

Purpose: compact human-readable companion to `docs/reports/hourly_review_state.json` for the recurring XerahS review.

Use `hourly_review_state.json` as the hot machine-readable source. The full historical ledger was preserved at `docs/reports/archive/hourly_review_tracker_2026-04-30.md`.

## Rules

- Read `docs/reports/hourly_review_state.json` first; use this file only for quick human context.
- Prefer the stalest high-priority area that is not blocked by a larger prerequisite.
- Record raw/noisy evidence as log paths only; do not paste full command output here.
- After each run, update the JSON area row, keep only a compact current summary here, and archive any long detail outside the hot files.

## Next Candidates

- OCR
- Tests / test discoverability
- Editor integration
- Plugin loading/runtime
- MCP server

## Current Coverage

| Area | Last Reviewed | Priority | Last Outcome | Follow-up |
|---|---|---|---|---|
| Capture pipeline | 2026-05-01 17:45 AWST | High | Fixed GDI fallback region normalization to match DXGI outward rounding/clamping and reject non-finite coordinates before integer casts; added regression coverage; bumped version `0.22.173` -> `0.22.174`. | Continue capture pipeline review around DXGI multi-monitor rotation/scaling edge cases, rotated display bounds, and cursor/selection parity. |
| OCR | 2026-04-30 06:41 AWST | High | Fixed onboarding OCR language refresh so removed language options are unsubscribed before replacement, preventing stale options from mutating selected languages after refresh. | Continue OCR review around onboarding selected-language collection replacement/unsubscription and platform OCR language refresh edge cases. |
| Settings/configuration | 2026-04-30 07:41 AWST | High | Fixed uploader config saves so SettingsChanged observers are notified for destination/provider changes; added regression coverage. | Continue settings review around async save completion semantics and custom config backup paths. |
| Assistant local memory/privacy/history | 2026-04-30 14:54 AWST | High | Fixed history file-path matching to use macOS case-insensitive semantics, so assistant OCR cache and privacy lookups find canonical history rows on default macOS volumes. | Continue assistant review around symlink-equivalent history paths and OCR cache invalidation when capture files are moved or deleted. |
| Tests / test discoverability | 2026-04-30 08:41 AWST | High | Marked main NUnit test discovery packages as private test/build assets so Microsoft.NET.Test.Sdk and NUnit3TestAdapter do not flow as normal transitive assets; added guardrail coverage. | Continue tests review around discovery package asset metadata for Avalonia.Headless.NUnit/coverage collectors and cross-target test host behavior. |
| Editor integration | 2026-04-30 09:43 AWST | High | Fixed editor window direct-close handling so ShowEditorSessionAsync completes with null instead of leaving callers awaiting forever; added regression coverage. | Continue editor integration review around Save/Save As result propagation and multi-image send-to sequencing. |
| Uploader core / plugin routing | 2026-04-30 16:50 AWST | High | Fixed encrypted Amazon S3 destination export so mobile .xsdc files require a configured bucket instead of exporting an incomplete destination; added regression coverage. | Continue uploader routing review around stale default-instance IDs, case-insensitive instance/category lookups, and mobile destination config validation parity. |
| Plugin loading/runtime | 2026-04-29 04:11 AWST | High | Fixed plugin package installation so a missing plugins root is created before installing a .xsdp package instead of failing with DirectoryNotFoundException. |  |
| FTP uploader plugin | 2026-05-01 15:35 AWST | Medium | Fixed legacy FTP public URL generation for bracketed IPv6 HttpHomePath values, preserving IPv6 hosts and optional ports; added regression coverage; bumped version `0.22.172` -> `0.22.173`. | Continue FTP uploader review around query-template URL generation, remote path normalization, and FTP/SFTP cancellation behavior. |
| Hotkeys/input | 2026-05-02 07:52 AWST | Medium | Fixed Wayland portal keypad shortcut accelerators to emit GTK/GDK keypad names (`KP_0`..`KP_9`) instead of display labels; added regression coverage; bumped version `0.22.180` -> `0.22.181`. | Continue hotkeys/input review around Wayland portal fallback state transitions, shortcut changed signal edge cases, and platform parity for modifier normalization. |
| Imgur uploader plugin | 2026-04-29 07:11 AWST | Medium | Fixed Imgur Client ID normalization before config save, login URL generation, uploader creation, and explorer auth setup. |  |
| Media subsystem | 2026-04-30 21:45 AWST | High | Fixed combined video thumbnail generation so skipped unreadable source images no longer shift timestamps onto later loaded images. | Continue media review around TakeThumbnails FFmpeg timeout/exit-code handling and mixed-dimension combined thumbnail layout. |
| MCP server | 2026-04-29 11:15 AWST | Medium | Fixed annotation renderer parameter parsing so malformed/scalar MCP annotation inputs are coerced or ignored safely. | Continue MCP review around RunTaskAsync upload result distinction and broader annotation schema validation. |
| CLI / command surface | 2026-04-29 14:05 AWST | Medium | Fixed CLI upload to use the direct upload processor/bootstrap readiness path and added uploader doctor/bootstrap commands. | Continue CLI review around uploader bootstrap provider choices and JSON doctor output contract. |
| Notifications/toasts | 2026-04-30 03:15 AWST | Medium | Fixed zero-duration auto-hide toasts so they start fade immediately instead of remaining visible indefinitely. | Toast opacity/fade behavior now has explicit zero-duration coverage; rotate through older tracker items next. |
| File/path handling | 2026-04-30 04:36 AWST | High | Fixed SettingsBase weekly backup scheduling so weekly-only configurations create backup archives. | Continue rotating through older settings/file persistence edge cases; consider checking backup retention pruning interactions next. |
| Indexer subsystem | 2026-04-29 18:17 AWST | Medium | Fixed negative MaxDepthLevel handling so non-positive depth is treated as unlimited instead of suppressing root files/folders. | Continue indexer review around unauthorized/path-too-long enumeration parity and output file collision handling. |
| Platform-specific services | 2026-05-01 01:45 AWST | High | Fixed macOS clipboard osascript launching to pass scripts via ArgumentList, drain stdout/stderr, and time out hung helper processes; added regression coverage; bumped version `0.22.165` -> `0.22.166`. | Continue platform-specific review around AppleScript file-list edge cases, macOS clipboard helper error surfacing, and Linux/Windows clipboard parity. |
| Region capture / window enumeration | 2026-05-01 13:35 AWST | High | Fixed Linux X11 frame extent expansion to ignore invalid `_NET_FRAME_EXTENTS` values that would overflow outer window bounds; added regression coverage; bumped version `0.22.171` -> `0.22.172`. | Continue region/window enumeration review around GNOME eval rect validation, X11 property conversion edge cases, and Wayland active-window fallback diagnostics. |

## Recent Runs

### 2026-05-01 17:45 AWST - Capture pipeline

- Area: Capture pipeline / Windows GDI fallback region normalization; files: `src/platform/XerahS.Platform.Windows/WindowsScreenCaptureService.cs`, `src/platform/XerahS.Platform.Windows/Capture/GdiCaptureRectHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Findings: GDI fallback truncated fractional `SKRect` edges and cast before finite validation, diverging from the safer DXGI crop path and potentially shrinking selected regions or mishandling invalid coordinates.
- Status: Fixed GDI capture rect creation to validate finite coordinates, floor/ceil outward, clamp against virtual desktop bounds, and reject empty/invalid captures; added regression coverage; bumped version `0.22.173` -> `0.22.174`.
- Build/test: `dotnet build` 0 warnings/0 errors, `dotnet test` 769 passed. Logs: `/tmp/xerahs-hourly-sweep/build-20260501-174155.log`, `/tmp/xerahs-hourly-sweep/test-20260501-174303.log`.
- Follow-up: Continue capture pipeline review around DXGI rotated/scaled display bounds and cursor/selection parity between modern and fallback capture paths.

### 2026-05-01 15:35 AWST - FTP uploader URL generation

- Area: FTP uploader plugin / legacy FTP URL generation (bracketed IPv6 `HttpHomePath`); files: `src/desktop/core/XerahS.Uploaders/LegacySupport/FileUploaders/FTPAccount.cs`, `tests/XerahS.Tests/Uploaders/FtpConfigViewModelTests.cs`, `Directory.Build.props`.
- Findings: bracketed IPv6 home paths could be misread by colon-as-port parsing, and name-parser URL encoding could turn brackets into `%5B/%5D`, causing generated public URLs to fail URI construction or lose host shape.
- Status: Fixed IPv6 host/port parsing and bracketed IPv6 URL rendering; added regression coverage; bumped version `0.22.172` -> `0.22.173`.
- Upstream: parent and `ShareX.ImageEditor` were already ahead/up to date with their upstream develop refs; no new upstream commits merged this run.
- Build/test: Release build 0 warnings/0 errors; tests passed 767 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260501-155129.log`, `/tmp/xerahs-hourly-sweep/test-20260501-155226.log`.
- Follow-up: Continue FTP uploader review around query-template URL generation, remote path normalization, and FTP/SFTP cancellation behavior.

### 2026-05-01 13:35 AWST - Region capture / window enumeration

- Area: Linux X11 frame extent overflow handling; files: `src/platform/XerahS.Platform.Linux/LinuxWindowService.cs`, `tests/XerahS.Tests/Platform/Linux/LinuxWindowServiceTests.cs`, `Directory.Build.props`.
- Findings: `_NET_FRAME_EXTENTS` is compositor-controlled metadata; very large values could overflow outer window bound expansion and wrap dimensions/origins before capture.
- Status: Fixed frame extent expansion to ignore invalid extents when calculated outer bounds overflow or become non-positive; added regression coverage; bumped version `0.22.171` -> `0.22.172`.
- Build/test: Release build 0 warnings/0 errors; tests passed 765 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260501-133751.log`, `/tmp/xerahs-hourly-sweep/test-20260501-134116.log`.
- Follow-up: Continue region/window enumeration review around GNOME eval rect validation, X11 property conversion edge cases, and Wayland active-window fallback diagnostics.

### 2026-05-01 01:45 AWST - Platform-specific services

- Area: Platform-specific services (macOS clipboard osascript helper launch/timeout); files: `MacOSClipboardService.cs`, `MacOSClipboardServiceTests.cs`, `Directory.Build.props`.
- Findings: `osascript` scripts were embedded in one quoted argument string and only stdout was read before waiting, leaving embedded quotes brittle and stderr-heavy failures able to hang clipboard operations.
- Status: Fixed helper launch to use `ArgumentList`, drain both streams, and kill timed-out helper processes; added regression coverage; bumped version `0.22.165` -> `0.22.166`.
- Build/Test: Release build and tests passed with zero warnings/errors; logs `/tmp/xerahs-hourly-sweep/build-20260501-013752.log`, `/tmp/xerahs-hourly-sweep/test-20260501-014133.log`.
- Follow-up: Continue platform-specific review around AppleScript file-list edge cases, macOS clipboard helper error surfacing, and Linux/Windows clipboard parity.

### 2026-04-30 16:50 AWST - Uploader core / plugin routing

- Area: Uploader core / plugin routing (encrypted S3 mobile destination config export)
- Files: `src/desktop/app/XerahS.UI/Services/DestinationConfigExportService.cs`, `tests/XerahS.Tests/Services/DestinationConfigExportServiceTests.cs`, `Directory.Build.props`
- Status: Fixed .xsdc export so Amazon S3 destinations must have a configured bucket before encrypted mobile export, preventing imported mobile configs with unusable blank buckets; added regression coverage.
- Version bump: 0.22.158 -> 0.22.159
- Validation: Release build/test passed (724 app tests, 14 MCP tests). Logs: /tmp/xerahs-hourly-sweep/build-20260430-164323.log, /tmp/xerahs-hourly-sweep/test-20260430-164438.log.
- Follow-up: Continue uploader routing review around stale default-instance IDs, case-insensitive instance/category lookups, and mobile destination config validation parity.

### 2026-04-30 15:46 AWST - Hotkeys/input

- Area: Hotkeys/input (Linux X11 modifier matching)
- Status: Fixed X11 hotkey dispatch so lock modifiers are ignored but extra modifiers no longer trigger narrower shortcuts; added regression coverage.
- Version bump: 0.22.157 -> 0.22.158
- Validation: Release build/test passed (717 app tests, 14 MCP tests). Logs: /tmp/xerahs-hourly-sweep/build-20260430-153842.log, /tmp/xerahs-hourly-sweep/test-20260430-154533.log.
- Follow-up: Continue hotkeys/input review around Wayland portal fallback state transitions and platform parity for modifier normalization.

### 2026-04-30 11:46 AWST - Media subsystem
- Area: Media subsystem (VideoThumbnailer random seek slots)
- Files: `src/desktop/core/XerahS.Media/VideoThumbnailer.cs`, `tests/XerahS.Tests/Tools/VideoThumbnailerTests.cs`, `Directory.Build.props`
- Finding: random thumbnail seeks rebuilt a slot list per thumbnail but chose a random inclusive slot index, which could ignore the current thumbnail segment, duplicate seek ranges, or select `slot + 1` past the list end.
- Fix: clamp the thumbnail index into its own segment, return 0 for too-short videos, and cover indexed/random short-video behavior with regression tests.
- Version: bumped `0.22.153` -> `0.22.154`.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260430-113716.log`).
- Tests: pass, 726 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260430-114228.log`).
- Follow-up: continue media review around TakeThumbnails FFmpeg timeout handling and combined-thumbnail timestamp/image alignment when some source thumbnails fail to load.

### 2026-04-30 09:43 AWST - Editor integration
- Topic: editor window direct-close completion
- Outcome: Direct window closes now complete editor sessions with null, preserving task-mode cancel/no-save handling; added regression coverage; bumped version to 0.22.152.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-093615.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-093931.log`

### 2026-04-30 08:41 AWST - Tests / test discoverability
- Topic: NUnit test discovery package asset metadata
- Outcome: Added PrivateAssets/all and explicit IncludeAssets for Microsoft.NET.Test.Sdk and NUnit3TestAdapter in XerahS.Tests; added regression guard; bumped version to 0.22.151.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-083931.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-084035.log`

### 2026-04-30 07:41 AWST - Settings/configuration
- Topic: uploader config save notifications
- Outcome: Raised SettingsChanged after uploader config saves so destination/provider changes notify settings observers; added regression coverage; bumped version to 0.22.150.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-073743.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-074103.log`

### 2026-04-30 06:41 AWST - OCR
- Topic: Onboarding OCR language refresh handlers
- Outcome: Unsubscribed removed onboarding OCR language options during platform language refresh and added regression coverage.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-063524.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-063846.log`

### 2026-04-30 05:41 AWST - Capture pipeline
- Topic: DXGI crop rectangle coordinate conversion
- Outcome: Preserved fractional capture bounds outward and avoided invalid/overflow-prone casts before clamping.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-054007.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-054110.log`

### 2026-04-30 04:36 AWST - File/path handling
- Topic: SettingsBase backup scheduling flags
- Outcome: Allowed weekly-only backup zip creation and added focused coverage.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-043410.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-043553.log`

### 2026-04-30 03:15 AWST - Notifications/toasts
- Topic: zero-duration auto-hide fade startup
- Outcome: Started fade immediately for valid zero-duration auto-hide toasts and added focused coverage.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-031132.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-031439.log`

### 2026-04-30 02:18 AWST - FTP uploader plugin
- Topic: remote upload directory retry path handling
- Outcome: Created missing retry directories from the parent remote path instead of bare filenames.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-021245.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-021556.log`

### 2026-04-30 01:20 AWST - Uploader core / plugin routing
- Topic: file-extension input normalization
- Outcome: Normalized caller-provided extensions for destination lookup and duplicate route checks.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-011652.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-012004.log`

### 2026-04-30 10:42 AWST - Plugin loading/runtime
- Area: Plugin loading/runtime (.xsdp package install/extraction)
- Files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginPackager.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`
- Finding: duplicate zip entries in a plugin package could be extracted with overwrite semantics after the manifest was preview-validated, allowing a later duplicate `plugin.json` or assembly entry to replace earlier content in the temp install tree.
- Fix: reject duplicate normalized archive entry paths during safe extraction and cover duplicate `plugin.json` packages with a regression test.
- Version: bumped `0.22.152` -> `0.22.153`.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260430-103803.log`).
- Tests: pass, 724 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260430-104119.log`).
- Follow-up: continue plugin runtime review around package entry canonicalization, duplicate directory/file collisions, and load-context unload diagnostics.

### 2026-04-30 12:45 AWST - Plugin loading/runtime
- Area: Plugin loading/runtime (.xsdp package install/extraction)
- Status: Fixed package extraction collision handling so archives cannot mix files and directories at the same canonical path or under a file parent; added regression coverage for file-then-nested-directory and directory-then-file collisions.
- Files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginPackager.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`
- Version: bumped patch 0.22.154 -> 0.22.155.
- Validation: Release build 0 warnings/errors; tests passed 728 total.
- Follow-up: continue plugin runtime review around package entry canonicalization edge cases and load-context unload diagnostics.

### 2026-04-30 13:45 AWST - FTP uploader plugin
- Area: FTP uploader plugin (SFTP key/password authentication fallback)
- Files: src/desktop/plugins/Ftp.Plugin/FtpUploader.cs; tests/XerahS.Tests/Uploaders/FtpConfigViewModelTests.cs; Directory.Build.props
- Status: Fixed SFTP client creation so an unreadable configured private key falls back to password auth when a password is available instead of failing before connection; added regression coverage for invalid-key/password fallback.
- Version bump: 0.22.155 -> 0.22.156.
- Validation: Release build 0 warnings/0 errors; Release tests passed 729 total.
- Logs: /tmp/xerahs-hourly-sweep/build-20260430-133655.log; /tmp/xerahs-hourly-sweep/test-20260430-134120.log
- Follow-up: continue FTP uploader review around remote path normalization, URL generation, and cancellation behavior during FTP/SFTP transfers.

### 2026-04-30 14:54 AWST - Assistant local memory/privacy/history
- Area: Assistant local memory/privacy/history (history path casing semantics)
- Files: `src/desktop/core/XerahS.History/HistoryManagerSQLite.cs`, `tests/XerahS.Tests/Assistant/HistoryManagerSQLiteTests.cs`, `Directory.Build.props`
- Finding: history file-path lookups used case-sensitive comparison on all non-Windows hosts, which misses existing rows on default case-insensitive macOS volumes when callers vary path casing.
- Fix: use case-insensitive comparison on macOS as well as Windows and update regression expectations; bumped patch version 0.22.156 -> 0.22.157.
- Validation: targeted history tests passed (6); Release build 0 warnings/0 errors; Release tests passed 729 total.
- Logs: `/tmp/xerahs-hourly-sweep/build-20260430-144539.log`; `/tmp/xerahs-hourly-sweep/test-20260430-144922.log`
- Follow-up: Continue assistant review around symlink-equivalent history paths and OCR cache invalidation when capture files are moved or deleted.

### 2026-04-30 17:45 AWST - Capture pipeline
- Area: macOS CLI region capture fallback; files: `CliCaptureStrategy.cs`, `MacOSRegionSelectorPreferenceTests.cs`, `Directory.Build.props`.
- Upstream: merged upstream/develop `87e57083` (native SwiftUI iOS shell) into develop; ShareX.ImageEditor healthy on `develop` (`360eeab`, origin current, no upstream incoming).
- Status: Fixed scaled macOS `screencapture -R` conversion to floor top-left and ceil bottom-right so fractional Retina bounds are not shrunk; added regression coverage.
- Version: bumped patch `0.22.159` -> `0.22.160`.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-173738.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-174057.log`.
- Follow-up: Continue capture review around multi-monitor logical origins and macOS permission/error fallback messages.

### 2026-04-30 18:45 AWST - Notifications/toasts
- Area: Native notification process launch; files: `LinuxNotificationService.cs`, `MacOSNotificationService.cs`, `NotificationServiceProcessStartInfoTests.cs`, `Directory.Build.props`.
- Findings: Linux/macOS notification helpers redirected stdout/stderr but never drained them, so noisy helpers could block and be killed as false timeouts.
- Status: Fixed native notification start info to avoid unread redirected pipes; added regression assertions; bumped version 0.22.160 -> 0.22.161.
- Upstream/submodules: parent and ShareX.ImageEditor develop were already up to date after fetch/merge; ShareX.ImageEditor stayed on `develop` at `360eeabe` with origin/upstream remotes verified.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-183728.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-184045.log`.
- Follow-up: Continue notification review around native action callback parity and OS helper availability/fallback messaging.

### 2026-04-30 19:41 AWST - FTP uploader URL generation
- Area: FTP uploader plugin (HTTP home path protocol-prefix normalization); files: `FTPAccount.cs`, `FtpConfigViewModelTests.cs`, `Directory.Build.props`.
- Upstream: merged upstream/develop `e43c28d2` into develop (merge `fabef0ea`); ShareX.ImageEditor checked out develop, origin `360eeab`, upstream `2144d8a`, no pointer change.
- Status: Fixed FTP/SFTP public URL generation so pasted full HttpHomePath values strip protocol prefixes before UriBuilder parsing, avoiding malformed hosts like `https:`; added regression coverage; bumped version 0.22.161 -> 0.22.162.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-193740.log` (0 warnings/errors); test `/tmp/xerahs-hourly-sweep/test-20260430-194116.log` (740 passed).
- Follow-up: Continue FTP uploader review around query-template URL generation and cancellation behavior during FTP/SFTP transfers.

### 2026-04-30 20:45 AWST - OCR tools result state
- Area: OCR tool/view-model result lifecycle; files: `OcrViewModel.cs`, `OcrViewModelTests.cs`, `Directory.Build.props`.
- Upstream: merged upstream/develop `d1dc65d5` into develop (merge `f84f564d`); ShareX.ImageEditor checked out `develop` at `360eeabe` with origin/upstream remotes verified and no pointer change.
- Status: Fixed stale OCR result state by clearing `HasResult` immediately when a new recognition pass starts, so copy/service-link UI cannot remain enabled for old text while processing; added async regression coverage; bumped version 0.22.162 -> 0.22.163.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-203937.log` (0 warnings/errors); test `/tmp/xerahs-hourly-sweep/test-20260430-204312.log` (754 passed total).
- Follow-up: Continue OCR/settings review around cancellation/rerun races and platform-specific OCR service error messaging.
### 2026-04-30 21:45 AWST - Media subsystem
- Area: Media subsystem (combined video thumbnail timestamp alignment); files: `VideoThumbnailer.cs`, `VideoThumbnailerTests.cs`, `Directory.Build.props`.
- Finding: combined thumbnail rendering skipped unreadable source images but still read timestamps by compacted image index, so later frames could display earlier/skipped timestamps.
- Status: Fixed loaded thumbnails to carry their originating timestamp through resize/render; added regression coverage; bumped version `0.22.163` -> `0.22.164`.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260430-213821.log`).
- Tests: pass, 755 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260430-214203.log`).
- Follow-up: Continue media review around TakeThumbnails FFmpeg timeout/exit-code handling and mixed-dimension combined thumbnail layout.


### 2026-04-30 23:45 AWST - Settings/configuration backups
- Area: Settings/configuration (daily + weekly settings backup retention); files: `SettingsBase.cs`, `SettingsManagerSecretsPathTests.cs`, `Directory.Build.props`.
- Status: Fixed settings saves so enabling both daily and weekly backup flags writes both backup zip variants instead of weekly suppressing the daily retention file; added regression coverage; bumped version `0.22.164` -> `0.22.165`.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-233736.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-234128.log`.
- Follow-up: Continue settings/configuration review around backup archive cleanup, custom backup folder validation, and save failure recovery after partial temp-file moves.

### 2026-05-01 03:35 AWST - Media subsystem
- Area: Media subsystem (VideoThumbnailer FFmpeg thumbnail timeout cleanup); files: `src/desktop/core/XerahS.Media/VideoThumbnailer.cs`, `tests/XerahS.Tests/Tools/VideoThumbnailerTests.cs`, `Directory.Build.props`.
- Finding: timed-out FFmpeg thumbnail extraction only waited 30s and then disposed the `Process`, which could leave a stuck FFmpeg process/tree running after the thumbnail attempt timed out.
- Fix: added timeout helper that kills the entire process tree and waits for exit; added regression coverage; bumped version `0.22.166` -> `0.22.167`.
- Upstream: merged `8f074e3e` (`[v0.22.153] [UI] Surface destination config export in provider settings`) into `develop`.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260501-033751.log`).
- Tests: pass, 758 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260501-034114.log`).
- Follow-up: continue media review around FFmpeg exit-code/error surfacing, partial thumbnail cleanup, and mixed-dimension combined thumbnail layout.

### 2026-05-01 05:43 AWST - Media subsystem
- Area: Media subsystem (combined video thumbnail mixed-size layout); files: `src/desktop/core/XerahS.Media/VideoThumbnailer.cs`, `tests/XerahS.Tests/Tools/VideoThumbnailerTests.cs`, `Directory.Build.props`.
- Finding: combined thumbnail sheets sized every grid cell from the first loaded image, so later larger thumbnails could overdraw/crop into adjacent cells or beyond the final bitmap.
- Fix: size combined grid cells from the largest loaded thumbnail dimensions while drawing each thumbnail's own border/shadow bounds; added regression coverage; bumped version `0.22.167` -> `0.22.168`.
- Upstream: parent and `ShareX.ImageEditor` were already up to date; no new upstream commits merged this run.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260501-053740.log`).
- Tests: pass, 759 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260501-054102.log`).
- Follow-up: continue media review around FFmpeg exit-code/error surfacing and partial thumbnail cleanup.

### 2026-05-01 07:42 AWST - Assistant local memory aliases
- Area: Assistant local memory/privacy/history (built-in alias execution path).
- Files: src/desktop/app/XerahS.Assistant/Services/AssistantLocalMemoryStore.cs; tests/XerahS.Tests/Assistant/AssistantServiceTests.cs; Directory.Build.props.
- Finding/fix: fixed the built-in `copy last five paths` alias so it actually requests clipboard copy instead of resolving to a non-copy lookup; added service coverage for clipboard behavior.
- Version: 0.22.168 -> 0.22.169.
- Validation: Release build 0 warnings/0 errors; full Release tests passed (746 XerahS.Tests + 14 XerahS.McpServer.Tests).
- Follow-up: continue rotating stale assistant memory/privacy/history prompts, especially alias phrases that imply side effects.

### 2026-05-01 09:41 AWST - File/path handling
- Area: File/path handling (unique file-name collision handling); files: `src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs`, `tests/XerahS.Tests/Helpers/FileHelpersTests.cs`, `Directory.Build.props`.
- Findings: `GetUniqueFilePath` only treated existing files as collisions, so an existing directory named like the requested output path could be returned unchanged or reused as a numbered candidate.
- Status: Fixed unique-name generation to use `Path.Exists` for initial and numbered candidates; added regression coverage for directory collisions; bumped version `0.22.169` -> `0.22.170`.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260501-093718.log`; test `/tmp/xerahs-hourly-sweep/test-20260501-094044.log`.
- Follow-up: Continue file/path review around copy/backup overwrite semantics, backup archive path validation, and file-lock behavior on missing paths.

### 2026-05-01 11:35 AWST - FTP uploader URL generation
- Area: FTP uploader plugin / legacy FTP URL generation (`@` HttpHomePath no-auto-subfolder marker); files: `src/desktop/core/XerahS.Uploaders/LegacySupport/FileUploaders/FTPAccount.cs`, `tests/XerahS.Tests/Uploaders/FtpConfigViewModelTests.cs`, `Directory.Build.props`.
- Findings: Legacy ShareX configs can prefix `HttpHomePath` with `@` to suppress automatic subfolder insertion, but XerahS stripped URL protocols without honoring the marker, producing URLs with duplicated/undesired subfolders.
- Status: Fixed `FTPAccount.GetUriPath` to strip the marker, disable subfolder auto-append for that URL calculation without mutating config, and preserve protocol-prefix cleanup; added regression coverage; bumped version `0.22.170` -> `0.22.171`.
- Validation: Release build and full Release no-build test suite passed (0 warnings/errors; 763 tests). Evidence: build `/tmp/xerahs-hourly-sweep/build-20260501-113828.log`; test `/tmp/xerahs-hourly-sweep/test-20260501-114242.log`.
- Follow-up: Continue FTP uploader review around query-template URL generation, remote path normalization, and FTP/SFTP cancellation behavior.

### 2026-05-01 19:35 AWST - Settings/configuration backups
- Area: Settings reset backup collision handling; files: `src/desktop/core/XerahS.Core/Managers/SettingsManager.cs`, `tests/XerahS.Tests/Helpers/SettingsManagerSecretsPathTests.cs`, `Directory.Build.props`.
- Findings: Two settings resets in the same second reused the same `Reset_yyyy-MM-dd_HH-mm-ss` backup folder, allowing the newer reset backup to overwrite files from the earlier reset.
- Status: Fixed reset backup creation to choose a suffixed unique folder on timestamp collision; added regression coverage; bumped version `0.22.174` -> `0.22.175`.
- Validation: Release build and full Release no-build test suite passed (0 warnings/errors; 770 tests). Evidence: build `/tmp/xerahs-hourly-sweep/build-20260501-194134.log`; test `/tmp/xerahs-hourly-sweep/test-20260501-194459.log`.
- Follow-up: Continue settings/configuration review around archived backup restore/fallback behavior and non-JSON companion-file coverage.

### 2026-05-01 21:35 AWST - Editor integration / image effect presets
- Area: Editor integration / `.xsie` image effect preset save path handling; files: `src/desktop/core/XerahS.Core/Helpers/ImageEffectPresetSerializer.cs`, `tests/XerahS.Tests/Helpers/ImageEffectPresetSerializerTests.cs`, `Directory.Build.props`.
- Status: Fixed preset saving to create missing parent folders before writing `Config.json` zip archives; added regression coverage; bumped version `0.22.175` -> `0.22.176`.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260501-213832.log`).
- Tests: pass, 771 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260501-214327.log`).
- Follow-up: Continue editor integration review around malformed preset archives, sidecar save error reporting, and video editor ffmpeg/ffprobe fallback diagnostics.

### 2026-05-01 23:50 AWST - Editor integration / malformed image effect presets
- Area: Editor integration / `.xsie` malformed preset load handling; files: `src/desktop/core/XerahS.Core/Helpers/ImageEffectPresetSerializer.cs`, `tests/XerahS.Tests/Helpers/ImageEffectPresetSerializerTests.cs`, `Directory.Build.props`.
- Findings: `LoadXsieFile` returned `null` for missing config but could still throw on corrupt zip payloads or invalid `Config.json`, allowing a bad preset file to break import/load flows.
- Status: Fixed preset loading to log and return `null` for malformed archives, invalid JSON, and IO/access failures; added regression coverage for corrupt archive and invalid config JSON; bumped version `0.22.176` -> `0.22.177`.
- Build/test: Release build succeeded with 0 warnings/errors; Release tests passed (`759` XerahS, `14` MCP) with logs under `/tmp/xerahs-hourly-sweep`.
- Follow-up: Continue editor integration review around sidecar save error reporting, unknown effect type UX, and video editor ffmpeg/ffprobe fallback diagnostics.

### 2026-05-02 01:50 AWST - Media subsystem / FFmpeg thumbnail failure handling
- Area: Media subsystem (VideoThumbnailer FFmpeg exit/timeout handling); files: `src/desktop/core/XerahS.Media/VideoThumbnailer.cs`, `tests/XerahS.Tests/Tools/VideoThumbnailerTests.cs`, `Directory.Build.props`.
- Status: Fixed thumbnail capture to delete stale deterministic output files before each FFmpeg run, only accept thumbnails from clean zero-exit FFmpeg runs, and clean partial files after failures/timeouts; added regression coverage for timeout return semantics; bumped version `0.22.177` -> `0.22.178`.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260502-014822.log`).
- Tests: pass, 774 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260502-014931.log`).
- Follow-up: Continue media review around partial thumbnail cleanup surfacing/logging and video editor ffmpeg/ffprobe fallback diagnostics.

### 2026-05-02 03:52 AWST - Uploader instance identity matching
- Area/files: `InstanceManager` lifecycle/default/routing ID lookups plus uploaders tests.
- Findings: uploader instance IDs are generated lowercase but external/config callers can provide equivalent mixed-case IDs; several lifecycle and routing exclusion paths compared IDs case-sensitively, causing missed updates/defaults/removals or false routing conflicts.
- Fix: normalized all instance ID equality checks through ordinal-ignore-case comparison and added regression coverage for get/update, default/duplicate/remove, and routing exclusion validation.
- Version: bumped patch to 0.22.179.
- Validation: Release build 0 warnings/0 errors; tests passed 777 total.
- Follow-up: continue rotating stale uploader/configuration surfaces for persistence/import edge cases.

### 2026-05-02 05:50 AWST - Assistant local memory aliases
- Area/files: assistant local alias persistence and built-in alias resolution; files: `src/desktop/app/XerahS.Assistant/Services/AssistantLocalMemoryStore.cs`, `tests/XerahS.Tests/Assistant/AssistantLocalMemoryStoreTests.cs`, `Directory.Build.props`.
- Findings: saved aliases using the same phrase as a built-in alias were shadowed forever by the built-in dictionary, so users could not override commands with side-effect wording such as "copy last five paths".
- Fix: resolve persisted aliases before falling back to built-ins and added regression coverage for user overrides; bumped version `0.22.179` -> `0.22.180`.
- Validation: Release build 0 warnings/0 errors; tests passed 778 total.
- Follow-up: continue assistant memory review around alias deletion/import semantics and symlink-equivalent history paths.
### 2026-05-02 07:52 AWST - Hotkeys/input
- Area/files: Wayland portal hotkey accelerator generation; files: `src/platform/XerahS.Platform.Linux/Services/WaylandPortalHotkeyService.cs`, `tests/XerahS.Tests/Platform/Linux/LinuxHotkeyServiceTests.cs`, `Directory.Build.props`.
- Findings: keypad digit shortcuts were serialized as human-facing `Numpad N` labels, not GTK/GDK accelerator names, so compositors could reject or ignore the requested portal binding.
- Fix: map keypad digits to `KP_0`..`KP_9`, add regression coverage for mapping and preferred trigger output, and bump version `0.22.180` -> `0.22.181`.
- Validation: Release build 0 warnings/0 errors; tests passed 780 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260502-074515.log`, `/tmp/xerahs-hourly-sweep/test-20260502-074902.log`.
- Follow-up: continue hotkeys/input review around Wayland portal fallback state transitions, shortcut changed signal edge cases, and platform parity for modifier normalization.

### 2026-05-02 09:43 AWST - Hotkeys/input
- Area/files: Wayland portal hotkey accelerator generation; files: `src/platform/XerahS.Platform.Linux/Services/WaylandPortalHotkeyService.cs`, `tests/XerahS.Tests/Platform/Linux/LinuxHotkeyServiceTests.cs`, `Directory.Build.props`.
- Findings: the Wayland portal emitted `Space` for spacebar shortcuts while GDK/X11 key names use lowercase `space`, creating a platform parity edge case where compositors may reject the accelerator.
- Fix: normalized Wayland portal spacebar accelerators to `space`, added regression coverage, and bumped version `0.22.181` -> `0.22.182`.
- Validation: blocked pending local exec approval for sync/build/test/push; latest sync command approval gate is `165b74a7` and build gate is `29f9c0d8`.
- Follow-up: once exec approval is available, complete upstream/submodule sync, Release build/test, and push the hotkey fix.

### 2026-05-02 11:52 AWST - Upstream sync + Hotkeys/input validation
- Area/files: upstream develop sync plus Wayland portal hotkey accelerator generation; files: `src/platform/XerahS.Platform.Linux/Services/WaylandPortalHotkeyService.cs`, `tests/XerahS.Tests/Platform/Linux/LinuxHotkeyServiceTests.cs`, `Directory.Build.props`, `ShareX.ImageEditor`.
- Findings: merged 4 upstream Android privacy/security commits into `develop`; ShareX.ImageEditor is healthy on `develop` with origin/upstream configured and no upstream-only commits. Spacebar accelerators needed GDK lowercase `space` parity for Wayland portal registration.
- Fix: completed/pushed the pending spacebar accelerator normalization with regression coverage and patch bump `0.22.181` -> `0.22.182`; parent includes upstream merge commit.
- Validation: Release build 0 warnings/0 errors; tests passed 781 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260502-114415.log`, `/tmp/xerahs-hourly-sweep/test-20260502-114826.log`.
- Follow-up: continue hotkeys/input review around Wayland portal fallback state transitions, shortcut changed signal edge cases, and platform parity for modifier normalization.

### 2026-05-02 14:02 AWST - FTP uploader URL generation
- Area: FTP uploader HTTP home/query URL generation after upstream sync.
- Files: `src/desktop/core/XerahS.Uploaders/LegacySupport/FileUploaders/FTPAccount.cs`, `tests/XerahS.Tests/Uploaders/FtpConfigViewModelTests.cs`, `Directory.Build.props`.
- Finding/Fix: Fixed query-template HTTP home paths ending in `=` so auto-added subfolders append to the query value without inserting a leading slash, and handled parser-encoded `%3F` query separators.
- Version: bumped `0.22.182` -> `0.22.183`.
- Validation: Release build 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260502-135840.log`); Release tests passed 782 total (`/tmp/xerahs-hourly-sweep/test-20260502-140021.log`).
- Follow-up: Continue rotating through uploader edge cases, especially FTP/SFTP path normalization and URL template parity.

### 2026-05-02 15:51 AWST - Assistant history path equivalence
- Area: Assistant local memory/privacy/history (symlink-equivalent history paths); files: `src/desktop/core/XerahS.History/HistoryManagerSQLite.cs`, `tests/XerahS.Tests/Assistant/HistoryManagerSQLiteTests.cs`, `Directory.Build.props`.
- Finding: history lookups canonicalized casing/full paths but did not compare symlink-resolved equivalents, so assistant OCR/history checks could miss captures accessed through a linked path.
- Fix: compare both full and final symlink target paths for history lookups; added regression coverage; bumped version `0.22.183` -> `0.22.184`.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260502-154623.log`).
- Tests: pass, 783 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260502-155038.log`).
- Follow-up: continue assistant memory review around alias deletion/import semantics and OCR cache invalidation when capture files are moved or deleted.

### 2026-05-02 17:55 AWST - Assistant local memory alias deletion
- Area: Assistant local memory/privacy/history (saved alias deletion semantics); files: `src/desktop/app/XerahS.Assistant/Services/AssistantLocalMemoryStore.cs`, `src/desktop/app/XerahS.Assistant/Services/AssistantService.cs`, `tests/XerahS.Tests/Assistant/AssistantLocalMemoryStoreTests.cs`, `Directory.Build.props`.
- Findings: Saved aliases could override built-in aliases but there was no safe local command to remove a saved alias and restore the built-in fallback.
- Status: Fixed assistant prompts such as `forget alias ...` / `delete alias ...` / `remove assistant alias ...` to delete saved aliases, report misses, and fall back to built-ins after override removal; added regression coverage; bumped version `0.22.184` -> `0.22.185`.
- Validation: Release build/test passed with zero warnings/errors; logs `/tmp/xerahs-hourly-sweep/build-20260502-174653.log`, `/tmp/xerahs-hourly-sweep/test-20260502-175017.log`.
- Follow-up: continue assistant memory review around alias import/export semantics and OCR cache invalidation when capture files are moved or deleted.

### 2026-05-02 19:54 AWST - Assistant OCR cache invalidation
- Area: Assistant local memory/privacy/history (OCR cache invalidation for moved/deleted capture files); files: `src/desktop/app/XerahS.Assistant/Services/AssistantHistoryService.cs`, `tests/XerahS.Tests/Assistant/AssistantHistoryServiceTests.cs`, `Directory.Build.props`.
- Findings: Assistant OCR actions could reuse cached history OCR text before checking that the referenced capture file still existed, and latest-screenshot history items could surface stale OCR text for unavailable files.
- Status: Fixed cached OCR reads/writes to require the local file to exist and hide OCR text on unavailable latest-screenshot entries; added regression coverage; bumped version `0.22.187` -> `0.22.188`.
- Validation: Release build/test passed with zero warnings/errors; logs `/tmp/xerahs-hourly-sweep/build-20260502-195213.log`, `/tmp/xerahs-hourly-sweep/test-20260502-195414.log`.
- Follow-up: continue assistant memory review around alias import/export semantics and history cleanup for moved files.

### 2026-05-02 21:56 AWST - Linux Flatpak/Snap runtime app id normalization
- Area: Linux Flatpak runtime compatibility / portal startup; files: `src/platform/XerahS.Platform.Linux/Services/LinuxRuntimeEnvironment.cs`, `src/platform/XerahS.Platform.Linux/Services/FlatpakPortalStartupService.cs`, `tests/XerahS.Tests/Platform/Linux/LinuxRuntimeEnvironmentTests.cs`, `tests/XerahS.Tests/Platform/Linux/FlatpakPortalServiceTests.cs`, `Directory.Build.props`.
- Findings: Upstream Flatpak support trusted raw sandbox app-id environment/config values; whitespace-only or padded IDs could produce invalid portal autostart command lines and blank Snap IDs.
- Status: Merged upstream `a2324c80` into `develop`; fixed sandbox app-id normalization for Flatpak/Snap detection and Flatpak autostart command generation; added regression coverage; bumped version `0.22.188` -> `0.22.189`; pushed `1bea7609` to `origin/develop`.
- Build/test: Release build 0 warnings/0 errors; Release tests passed 796 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260502-214611.log`, `/tmp/xerahs-hourly-sweep/test-20260502-214934.log`.
- Submodule: `ShareX.ImageEditor` verified on `develop`, origin/upstream remotes corrected, fetched both; origin/develop unchanged at `360eeabe`, fork remains 1 ahead of upstream with no pointer update needed.
- Follow-up: continue Linux Flatpak portal review around portal notification payload variants, startup marker reconciliation after denied requests, and Wayland portal fallback diagnostics.

### 2026-05-02 23:56 AWST - Linux Flatpak portal autostart state
- Area: Linux Flatpak/XDG portal startup state after upstream Flatpak/XDG sync.
- Files reviewed: `FlatpakPortalStartupService.cs`, Linux portal service tests, wallpaper XDG cache test.
- Findings: Flatpak autostart marker was stored under XDG config even though it is mutable runtime state; stale config marker could also survive disable after moving storage.
- Fix: moved new marker writes to XDG state, kept legacy config-marker reads for migration, deletes both paths on disable/new write, and updated tests; bumped version to 0.22.190.
- Build/test: Release build 0 warnings/0 errors; Release tests passed (789 + 14). Logs: `/tmp/xerahs-hourly-sweep/build-20260502-235518.log`, `/tmp/xerahs-hourly-sweep/test-20260502-235626.log`.
- Follow-up: Continue Linux portal UX review around Background portal denial/cancellation diagnostics.

### 2026-05-03 01:56 AWST - Plugin loading/runtime preview manifest validation
- Area: Plugin loading/runtime (`.xsdp` preview/install manifest entry parity); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginPackager.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Findings: `PreviewPackage` trusted the first `plugin.json` entry while install-time safe extraction rejected duplicate entries later, letting installer UI preview ambiguous/tampered package metadata.
- Status: Fixed manifest lookup to require a single manifest entry for preview and install, added duplicate-manifest preview regression coverage, and bumped version `0.22.190` -> `0.22.191`.
- Validation: Release build and full Release test suite passed with zero warnings/errors; logs under `/tmp/xerahs-hourly-sweep/`.
- Follow-up: continue plugin runtime review around package entry casing/canonicalization parity and load-context unload diagnostics.

### 2026-05-03 03:53 AWST - Plugin loading/runtime manifest casing parity
- Area: Plugin loading/runtime (`.xsdp` manifest entry canonicalization); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginPackager.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Upstream sync: `develop` already contained `upstream/develop` (`2914c22d`) and remained 4 commits ahead of upstream / even with origin; ShareX.ImageEditor stayed on `develop` at `360eeab`, remotes verified, no parent pointer change needed.
- Status: Fixed preview/install manifest lookup to reject case-variant non-canonical `plugin.json` entries instead of preview silently ignoring packages that would collide on case-insensitive extraction; added regression coverage; bumped version `0.22.191` -> `0.22.192`.
- Build/test: Release build passed with 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260503-034510.log`); Release tests passed 805 total (`/tmp/xerahs-hourly-sweep/test-20260503-034826.log`).
- Follow-up: continue plugin runtime review around package path segment canonicalization beyond root manifest casing and load-context unload diagnostics.

### 2026-05-03 05:54 AWST - Plugin loading/runtime package entry path canonicalization
- Area: Plugin loading/runtime (`.xsdp` extraction entry path canonicalization); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginPackager.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Findings: `.xsdp` install extraction canonicalized target paths after `Path.GetFullPath`, allowing ambiguous archive names such as `assets/../sample-plugin.dll` to install as valid files and backslash-separated entries to behave differently across platforms.
- Status: Fixed install extraction to reject rooted, backslash, empty, `.`, and `..` entry path segments before extraction; added regression coverage for dot-dot and backslash entries; bumped version `0.22.192` -> `0.22.193`.
- Upstream/Submodules: Parent `upstream/develop` had no new commits to merge; `ShareX.ImageEditor` stayed on `develop` at `360eeab`, origin/upstream fetched and already contained upstream `2144d8a`.
- Validation: Release build and tests passed with zero warnings/errors. Logs: `/tmp/xerahs-hourly-sweep/build-20260503-054454.log`, `/tmp/xerahs-hourly-sweep/test-20260503-054812.log`.
- Follow-up: continue plugin runtime review around load-context unload diagnostics and package preview/install parity for non-root asset metadata.

### 2026-05-03 07:56 AWST - Plugin loading/runtime load-context snapshot
- Area: Plugin loading/runtime (`PluginLoader.GetLoadedContexts` external mutation hardening); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: `GetLoadedContexts()` returned the loader's live dictionary behind an `IReadOnlyDictionary`, so diagnostic/preview callers could cast and mutate it, losing unload handles and leaking collectible contexts.
- Status: Fixed the API to return a read-only snapshot, added regression coverage that external `Clear()` is rejected while unload still succeeds, and bumped version `0.22.193` -> `0.22.194`.
- Upstream/submodules: parent already ahead of `upstream/develop` with no new upstream commits; `ShareX.ImageEditor` remotes verified (`origin` KovaForge, `upstream` ShareX), on `develop`, no pointer change.
- Validation: Release build passed with 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260503-075142.log`); Release tests passed 808 total, 0 failed (`/tmp/xerahs-hourly-sweep/test-20260503-075425.log`).
- Follow-up: continue plugin runtime review around load-context unload diagnostics and package preview/install parity for non-root asset metadata.

### 2026-05-03 11:55 AWST - Plugin loading/runtime preview package asset path parity
- Area: Plugin loading/runtime (`.xsdp` preview validation for non-root asset entries); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginPackager.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Findings: Preview validation parsed the manifest but did not apply install-time canonical entry/path-collision checks to non-root package assets, so malformed packages could preview successfully and only fail during install.
- Status: Fixed preview to validate all archive entry paths, duplicate/case-insensitive collisions, and file-vs-directory parent collisions before reading manifest metadata; added regressions for dot-dot asset paths and file-then-nested asset collisions; bumped version `0.22.194` -> `0.22.195`.
- Validation: Release build/test passed with 0 warnings; logs: `/tmp/xerahs-hourly-sweep/build-20260503-115628.log`, `/tmp/xerahs-hourly-sweep/test-20260503-115950.log`.
- Follow-up: continue plugin runtime review around load-context unload diagnostics and package preview/install parity for declared assembly/dependency asset existence.

### 2026-05-03 14:05 AWST - Plugin loading/runtime declared package asset validation
- Area: Plugin loading/runtime (`.xsdp` manifest-declared assembly/dependency entries); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginPackager.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Findings: Package preview could accept manifests whose declared assembly was absent, and install did not reject missing/non-canonical manifest dependency assets before extraction.
- Status: Fixed preview/install validation to require the manifest assembly and each declared dependency to exist as canonical file entries in the package; added regression coverage; bumped version `0.22.195` -> `0.22.196`.
- Upstream/Submodule: Parent `develop` already contained upstream `2914c22d`; `ShareX.ImageEditor` checked out on `develop` at `360eeab` with origin/upstream healthy and no parent pointer change.
- Build/Test: Release build passed with 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260503-140305.log`); Release tests passed 814 total (`/tmp/xerahs-hourly-sweep/test-20260503-140500.log`).
- Follow-up: continue plugin runtime review around load-context unload diagnostics and richer dependency metadata validation/error messaging.

### 2026-05-03 16:03 AWST - Plugin loading/runtime dependency metadata validation
- Area: Plugin loading/runtime (`.xsdp` manifest dependency metadata); files: `src/desktop/core/XerahS.UploaderPluginSdk/PluginManifest.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Findings: Dependency lists loaded from package manifests could be `null` or include blank entries; preview/install validation then either risked a null-reference path or silently ignored invalid dependency metadata.
- Status: Fixed manifest validation to reject null dependency lists and empty dependency values, added preview regression coverage, and bumped version `0.22.196` -> `0.22.197`.
- Upstream/Submodules: Parent `develop` and `ShareX.ImageEditor` were already current with upstream/origin; submodule remains on `develop` at `360eeabe`.
- Build/Test: Release build succeeded with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260503-155807.log`); Release tests passed 816/816 (`/tmp/xerahs-hourly-sweep/test-20260503-160122.log`).
- Follow-up: continue plugin runtime review around load-context unload diagnostics and richer dependency metadata validation/error messaging.

### 2026-05-03 18:01 AWST - Plugin loading/runtime dependency path metadata validation
- Area: Plugin loading/runtime (`.xsdp` manifest dependency path metadata); files: `src/desktop/core/XerahS.UploaderPluginSdk/PluginManifest.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Status: Synced upstream/develop (already contained) and ShareX.ImageEditor develop (already contained); fixed manifest validation to reject non-canonical/rooted dependency paths before preview/install asset lookup; added regression coverage; bumped version `0.22.197` -> `0.22.198`.
- Build/test: Release build 0 warnings/0 errors; tests passed 818 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260503-180355.log`, `/tmp/xerahs-hourly-sweep/test-20260503-180458.log`.
- Follow-up: continue plugin runtime review around load-context unload diagnostics and dependency metadata.

### 2026-05-03 20:02 AWST - Plugin loading/runtime installed-folder cleanup dependency metadata
- Area: Plugin loading/runtime (`PluginFolderCleaner` handling of manifest-declared dependency paths); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginFolderCleaner.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Upstream sync: `origin/develop` and `upstream/develop` already contained at parent `bb92deb1`; `ShareX.ImageEditor` develop already contained at `360eeab` with origin/upstream remotes normalized.
- Status: Fixed installed plugin cleanup so unsafe/non-canonical manifest dependency paths are ignored instead of preserving unexpected files during quarantine cleanup; added regression coverage; bumped version `0.22.198` -> `0.22.199`.
- Validation: Release build succeeded with 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260503-195905.log`); Release tests passed 819 total (`/tmp/xerahs-hourly-sweep/test-20260503-200230.log`).
- Follow-up: continue plugin runtime review around deps.json asset path canonicalization and load-context unload diagnostics.

### 2026-05-03 22:00 AWST - Plugin loading/runtime deps.json asset path canonicalization
- Area: Plugin loading/runtime (`PluginFolderCleaner` handling of deps.json asset paths); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginFolderCleaner.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`.
- Upstream: XerahS `upstream/develop` already contained; ShareX.ImageEditor `upstream/develop` already contained; submodule stayed on `develop` at `360eeab`.
- Status: Fixed plugin cleanup to ignore unsafe/non-canonical `.deps.json` runtime/native/resource asset paths instead of preserving unexpected files; added regression coverage; bumped version `0.22.199` -> `0.22.200`.
- Validation: Release build 0 warnings/0 errors; Release tests passed 820 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260503-215639.log`, `/tmp/xerahs-hourly-sweep/test-20260503-215954.log`.
- Follow-up: continue plugin runtime review around load-context unload diagnostics and plugin dependency resolution error messaging.

### 2026-05-04 00:00 AWST - Plugin loading/runtime unload request validation
- Area: Plugin loading/runtime (`PluginLoader.UnloadPlugin` invalid provider ids); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: `UnloadPlugin(null)` could throw from dictionary lookup and blank ids were treated as real lookup keys, which made defensive unload/error paths less robust.
- Status: Fixed unload requests to return `false` for null/blank provider ids without mutating loaded contexts; added regression coverage; bumped version `0.22.200` -> `0.22.201`.
- Build/test: Release build succeeded with 0 warnings; Release tests passed (821 total). Logs: `/tmp/xerahs-hourly-sweep/build-20260503-235634.log`, `/tmp/xerahs-hourly-sweep/test-20260503-235945.log`.
- Follow-up: continue plugin runtime review around load-context unload diagnostics and plugin dependency resolution error messaging.

### 2026-05-04 02:00 AWST - Plugin loading/runtime missing assembly diagnostics
- Area: Plugin loading/runtime (`PluginLoader.LoadPlugin` missing assembly diagnostics); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Status: Synced upstream/develop (already contained) and ShareX.ImageEditor develop (already contained); fixed missing plugin assembly loads to report `Assembly not found` directly instead of architecture-inspection failure; added regression coverage; bumped version `0.22.201` -> `0.22.202`.
- Validation: Release build succeeded with 0 warnings/0 errors; Release tests passed 822 total.
- Follow-up: continue plugin runtime review around dependency resolution diagnostics and load-context unload diagnostics.

### 2026-05-04 04:02 AWST - Plugin loading/runtime blank provider id validation
- Area: Plugin loading/runtime (`PluginLoader.LoadPlugin` blank runtime provider ids); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: A plugin whose provider returned a null/blank runtime `ProviderId` could be instantiated and then fail or leave an unusable unload key during context tracking.
- Status: Synced upstream/develop (already contained) and ShareX.ImageEditor develop (already contained); fixed load validation to reject blank provider ids with a clear load error and unload the failed context; added regression coverage; bumped version `0.22.202` -> `0.22.203`.
- Validation: Release build/test passed with 0 warnings/errors; logs `/tmp/xerahs-hourly-sweep/build-20260504-035715.log`, `/tmp/xerahs-hourly-sweep/test-20260504-040024.log`.
- Follow-up: continue plugin runtime review around dependency resolution diagnostics and load-context unload diagnostics.

### 2026-05-04 06:02 AWST - Plugin loading/runtime dependency diagnostics
- Area: Plugin loading/runtime (`PluginLoader.LoadPlugin` dependency resolution diagnostics); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop (`eabdc09b`) into `develop`; ShareX.ImageEditor develop was already current. Fixed provider activation failures caused by missing dependencies to report `Dependency not found` instead of a misleading assembly/unexpected error; added regression coverage; bumped version `0.22.203` -> `0.22.204`.
- Build/test: Release build 0 warnings/0 errors; tests passed (824 total). Logs: `/tmp/xerahs-hourly-sweep/build-20260504-055654.log`, `/tmp/xerahs-hourly-sweep/test-20260504-060004.log`.
- Follow-up: continue plugin runtime review around load-context unload diagnostics and plugin dependency resolution error messaging for type-load/reflection loader exceptions.

### 2026-05-04 08:03 AWST - Plugin loading/runtime type-load diagnostics

- Area: Plugin loading/runtime (`PluginLoader.LoadPlugin` type/reflection loader diagnostics); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Provider activation failures that surfaced `TypeLoadException` or `ReflectionTypeLoadException` through `TargetInvocationException` were reported as generic unexpected errors, and direct reflection loader failures omitted `LoadError` loader details.
- Status: Merged upstream/develop (`523099ed`), resolved the 2026-05-03 blog add/add conflict by keeping KovaForge's populated draft, and confirmed ShareX.ImageEditor develop was current. Fixed type/reflection loader diagnostics to preserve actionable load errors and loader-exception messages; added regression coverage; bumped version `0.22.204` -> `0.22.205`.
- Build/Test: Release build succeeded with 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260504-075714.log`); Release tests passed 826/826 (`/tmp/xerahs-hourly-sweep/test-20260504-080035.log`).
- Follow-up: continue plugin runtime review around unload/collectibility diagnostics and dependency resolution error messaging for load-context resolver failures.

### 2026-05-04 10:03 AWST - Plugin loading/runtime dependency load diagnostics
- Area: Plugin loading/runtime (`PluginLoader.LoadPlugin` dependency load failures); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Dependency load failures raised as `FileLoadException` during provider activation fell through to the generic unexpected-error path, hiding the dependency filename and actionable loader message.
- Status: Synced upstream/develop (already contained) and ShareX.ImageEditor develop (already contained). Fixed `FileLoadException` handling to report `Dependency load failed: <assembly>: <message>` and keep failed contexts untracked; added regression coverage; bumped version `0.22.205` -> `0.22.206`.
- Validation: Release build succeeded with 0 warnings/errors; Release tests passed (827 total). Logs: `/tmp/xerahs-hourly-sweep/build-20260504-095747.log`, `/tmp/xerahs-hourly-sweep/test-20260504-100057.log`.
- Follow-up: continue plugin runtime review around unload/collectibility diagnostics and resolver failures that surface outside provider activation.

### 2026-05-04 12:03 AWST - Plugin loading/runtime bad image diagnostics
- Area: Plugin loading/runtime (`PluginLoader.LoadPlugin` provider activation bad image failures); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Provider activation can surface `BadImageFormatException` through `TargetInvocationException`, which previously fell through to an `Unexpected error` load message.
- Status: Synced upstream/develop and ShareX.ImageEditor develop (both already contained). Fixed activation-time bad-image failures to report `Invalid or incompatible assembly image`, added regression coverage, and bumped version `0.22.206` -> `0.22.207`.
- Build/Test: Release build 0 warnings/0 errors; Release tests passed (828 total). Logs: `/tmp/xerahs-hourly-sweep/build-20260504-115659.log`, `/tmp/xerahs-hourly-sweep/test-20260504-120011.log`.
- Follow-up: continue plugin runtime review around unload/collectibility diagnostics and resolver failures that surface before entry-point discovery.

### 2026-05-04 14:02 AWST - Plugin loading/runtime provider-id casing unload
- Area: Plugin loading/runtime (`PluginLoader` provider-id casing); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Status: Synced upstream/develop and ShareX.ImageEditor develop (both already contained). Fixed loaded-context tracking to compare provider IDs case-insensitively, preserving case-insensitive unload/replacement and snapshot lookups for provider-id casing variants; added regression coverage; bumped version `0.22.207` -> `0.22.208`.
- Build/test: Release build 0 warnings/0 errors; Release tests 829 passed. Logs: `/tmp/xerahs-hourly-sweep/build-20260504-135732.log`, `/tmp/xerahs-hourly-sweep/test-20260504-140044.log`.
- Follow-up: continue plugin runtime review around unload/collectibility diagnostics and resolver failures that surface before entry-point discovery.

### 2026-05-04 16:01 AWST - Plugin loading/runtime provider catalog ID casing
- Area: Plugin loading/runtime (ProviderCatalog provider-id lookup casing); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Status: Synced upstream/develop and ShareX.ImageEditor develop (both already contained). Fixed ProviderCatalog provider/metadata dictionaries to use case-insensitive provider IDs so registration, lookup, reload removal, and metadata access match PluginLoader unload semantics; added regression coverage; bumped version `0.22.208` -> `0.22.209`.
- Validation: Release build/test passed with 0 warnings; logs `/tmp/xerahs-hourly-sweep/build-20260504-155747.log`, `/tmp/xerahs-hourly-sweep/test-20260504-160100.log`.
- Follow-up: continue plugin runtime review around unload/collectibility diagnostics and resolver failures that surface before entry-point discovery.

### 2026-05-04 18:02 AWST - Plugin loading/runtime blank provider-id catalog lookups
- Area: Plugin loading/runtime (ProviderCatalog blank provider-id lookup guards); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Status: Synced upstream/develop and ShareX.ImageEditor develop (both already contained). Fixed ProviderCatalog `GetProvider`, `GetPluginMetadata`, and `GetExplorer` to return null for null/blank provider IDs instead of letting dictionary lookups throw; added regression coverage; bumped version `0.22.209` -> `0.22.210`.
- Build/Test: Release build zero warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260504-175713.log`); Release tests passed 831 total (`/tmp/xerahs-hourly-sweep/test-20260504-180028.log`).
- Follow-up: continue plugin runtime review around unload/collectibility diagnostics and resolver failures that surface before entry-point discovery.

### 2026-05-04 20:02 AWST - Plugin loading/runtime programmatic provider registration blank IDs
- Area: Plugin loading/runtime (`ProviderCatalog.RegisterProvider` provider-id guards); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Programmatic provider registration did not mirror runtime blank provider-id guards; a blank/null-like provider ID could reach dictionary registration and throw or pollute catalog state.
- Status: Synced upstream/develop and ShareX.ImageEditor develop (both already contained). Fixed registration to ignore providers with missing provider IDs, added regression coverage, and bumped version `0.22.210` -> `0.22.211`.
- Validation: Release build/test passed with 0 warnings/errors; tests passed 832 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260504-195723.log`, `/tmp/xerahs-hourly-sweep/test-20260504-195723.log`.
- Follow-up: continue plugin runtime review around unload/collectibility diagnostics and resolver failures that surface before entry-point discovery.

### 2026-05-05 12:06 AWST - Plugin loading/runtime manifest duplicate guard
- Area: Plugin loading/runtime (`ProviderCatalog` duplicate manifest tracking for mismatched runtime provider IDs); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Duplicate-discovery checks only looked up manifest IDs as dictionary keys, but successful mismatched plugins store metadata under runtime provider ID; the same manifest plugin ID could be loaded again.
- Status: Fast-forwarded local `develop` to `origin/develop`; upstream/develop and ShareX.ImageEditor develop were already contained/current. Fixed loaded-plugin duplicate checks to find manifest IDs even when metadata is keyed by runtime provider ID; added regression coverage; bumped version `0.22.211` -> `0.22.212`.
- Validation: Release build passed with 0 warnings/errors; tests passed 833 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260505-120123.log`, `/tmp/xerahs-hourly-sweep/test-20260505-120527.log`.
- Follow-up: continue plugin runtime review around unload/collectibility diagnostics and resolver failures that surface before entry-point discovery.

### 2026-05-05 16:04 AWST - Plugin loading/runtime static loader cleanup
- Area: Plugin loading/runtime (`ProviderCatalog.Clear` static loader context cleanup); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: `ProviderCatalog.Clear()` reset providers and metadata but left the static `PluginLoader` holding loaded collectible contexts, which could keep plugin assemblies/handles alive after resets.
- Status: Upstream/develop and ShareX.ImageEditor develop were already contained/current; origin push remains blocked by missing GitHub HTTPS credentials. Fixed `Clear()` to unload retained static plugin loader contexts, added regression coverage, and bumped version `0.22.212` -> `0.22.213`.
- Validation: Release build passed with 0 warnings/errors; tests passed 834 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260505-155954.log`, `/tmp/xerahs-hourly-sweep/test-20260505-160312.log`.
- Follow-up: unblock GitHub credentials and push the two local commits; continue plugin runtime review around unload/collectibility diagnostics and resolver failures that surface before entry-point discovery.

### 2026-05-05 20:03 AWST - Plugin loading/runtime failed-load collectibility
- Area: Plugin loading/runtime (`PluginLoader` failed context cleanup after missing entry points); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Failed plugin load paths unloaded collectible contexts but skipped the forced collection pass used by normal unloads, leaving failed-load cleanup weaker than successful unload cleanup.
- Status: Upstream/develop and ShareX.ImageEditor develop were already contained/current; origin push remains blocked by missing GitHub HTTPS credentials. Fixed failed plugin load cleanup to force collection after unloading failed contexts, added missing-entry-point regression coverage, and bumped version `0.22.213` -> `0.22.214`.
- Validation: Release build passed with 0 warnings/errors; tests passed 835 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260505-195853.log`, `/tmp/xerahs-hourly-sweep/test-20260505-200254.log`.
- Follow-up: unblock GitHub credentials and push the three local commits; continue plugin runtime review around resolver failures that surface before entry-point discovery.

### 2026-05-06 00:06 AWST - Plugin loading/runtime missing dependency diagnostics
- Area: Plugin loading/runtime (`PluginLoader` missing dependency error formatting); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: `FileNotFoundException` diagnostics assumed `FileName` was populated, so resolver/load failures without a file name produced an empty `Dependency not found:` message.
- Status: Merged upstream/develop `09e5a718` into local `develop`, keeping KovaForge blog conflict content; ShareX.ImageEditor remotes/branch verified current. Fixed missing-dependency formatting to report `unknown assembly` plus the exception message, added regression coverage, bumped version `0.22.214` -> `0.22.215`, and pushed `develop` to origin through `4c62eaa4`.
- Validation: Release build passed with 0 warnings/errors; tests passed 836 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260506-000022.log`, `/tmp/xerahs-hourly-sweep/test-20260506-000350.log`.
- Follow-up: continue plugin runtime review around resolver failures before entry-point discovery.

### 2026-05-06 04:04 AWST - Plugin loading/runtime reflection loader diagnostics
- Area: Plugin loading/runtime (`PluginLoader` reflection loader exception formatting); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Reflection loader diagnostics appended raw nested loader messages, so missing/load-failed dependency assembly names could be omitted from the user-facing `LoadError`.
- Status: Upstream/develop and origin/develop were already contained; ShareX.ImageEditor develop/remotes verified current. Fixed nested reflection loader exception formatting to reuse dependency/type/bad-image diagnostics, added regression coverage, and bumped version `0.22.215` -> `0.22.216`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260506-035759.log`); tests passed 836 total (`/tmp/xerahs-hourly-sweep/test-20260506-040112.log`).
- Follow-up: continue plugin runtime review around resolver failures before entry-point discovery, especially direct `AssemblyLoadContext` resolver errors.

### 2026-05-06 08:08 AWST - Plugin loading/runtime private dependency fallback
- Area: Plugin loading/runtime (`PluginLoadContext` dependency resolution); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoadContext.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: The load context kept the plugin directory but only used `AssemblyDependencyResolver`, so plugin-private DLLs copied beside the plugin could fail when absent from the resolver graph.
- Status: Upstream/develop and origin/develop were already contained; ShareX.ImageEditor develop/remotes verified current. Fixed resolver fallback to load safe same-directory plugin-private assemblies, added regression coverage, bumped version `0.22.216` -> `0.22.217`, and pushed `develop` through `44ab0f4c`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260506-080604.log`); tests passed 837 total (`/tmp/xerahs-hourly-sweep/test-20260506-080711.log`).
- Follow-up: continue plugin runtime review around unmanaged DLL resolver fallback and dependency diagnostics.

### 2026-05-06 12:06 AWST - Plugin loading/runtime unmanaged dependency fallback
- Area: Plugin loading/runtime (`PluginLoadContext` unmanaged dependency resolution); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoadContext.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Unmanaged plugin dependencies only used `AssemblyDependencyResolver`, so native libraries copied beside a plugin could fail to resolve when absent from `.deps.json`.
- Status: Fast-forwarded local `develop` to `origin/develop`; upstream/develop already contained. ShareX.ImageEditor develop/remotes verified current. Fixed unmanaged DLL fallback to resolve safe same-directory native library names, added regression coverage, and bumped version `0.22.217` -> `0.22.218`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260506-120042.log`); tests passed 838 total (`/tmp/xerahs-hourly-sweep/test-20260506-120405.log`).
- Follow-up: continue plugin runtime review around dependency diagnostics beyond resolver fallback.

### 2026-05-06 16:02 AWST - Plugin loading/runtime assembly identity fallback
- Area: Plugin loading/runtime (`PluginLoadContext` managed dependency resolution); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoadContext.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: The plugin-directory managed DLL fallback accepted a DLL by filename only, so a mismatched assembly copied under the requested name could be loaded as the dependency.
- Status: Upstream/develop and origin/develop were already contained; ShareX.ImageEditor develop/remotes verified current. Fixed fallback to verify assembly identity before loading, added mismatch regression coverage, and bumped version `0.22.218` -> `0.22.219`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260506-155910.log`); tests passed 839 total (`/tmp/xerahs-hourly-sweep/test-20260506-160226.log`).
- Follow-up: continue plugin runtime review around dependency diagnostics beyond resolver fallback and version/culture edge cases.

### 2026-05-06 20:09 AWST - Plugin loading/runtime assembly culture identity
- Area: Plugin loading/runtime (`PluginLoadContext` managed dependency identity); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoadContext.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: The plugin-directory fallback treated a neutral-culture request as compatible with a satellite/resource assembly when simple name and version matched.
- Status: Upstream/develop and origin/develop were already contained; ShareX.ImageEditor develop/remotes verified current. Fixed fallback identity matching to require exact culture parity, added regression coverage, and bumped version `0.22.219` -> `0.22.220`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260506-200548.log`); tests passed 840 total (`/tmp/xerahs-hourly-sweep/test-20260506-200852.log`).
- Follow-up: continue plugin runtime review around dependency diagnostics beyond resolver fallback and remaining public-key/version edge cases.

### 2026-05-07 00:05 AWST - Plugin loading/runtime public key identity
- Area: Plugin loading/runtime (`PluginLoadContext` managed dependency identity); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoadContext.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: The plugin-directory fallback accepted a strong-named DLL for an unsigned/simple-name request when name, version, and culture matched, allowing a non-identical assembly identity to bind from the plugin folder.
- Status: Merged upstream/develop docs commits `9f90e073` and `925ce5c1`, keeping KovaForge's fuller blog drafts during conflicts; ShareX.ImageEditor develop/remotes verified current. Fixed fallback identity matching to require exact public-key-token parity, updated identity regressions, and bumped version `0.22.220` -> `0.22.221`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260507-000245.log`); tests passed 841 total (`/tmp/xerahs-hourly-sweep/test-20260507-000245.log`).
- Follow-up: continue plugin runtime review around dependency diagnostics beyond resolver fallback and remaining version/public-key edge cases.

### 2026-05-07 04:02 AWST - Plugin loading/runtime shared dependency casing
- Area: Plugin loading/runtime (`PluginLoadContext` shared dependency fallback); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoadContext.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: Shared dependency detection was case-sensitive even though assembly identity matching is case-insensitive, so lowercase `system.*` requests could reach plugin-directory fallback instead of staying in the host context.
- Status: Upstream/develop and origin/develop already contained; ShareX.ImageEditor develop/remotes verified current. Fixed shared dependency checks to use ordinal-ignore-case comparisons/prefixes, added regression coverage, and bumped version `0.22.221` -> `0.22.222`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260507-035758.log`); tests passed 842 total (`/tmp/xerahs-hourly-sweep/test-20260507-040110.log`).
- Follow-up: continue plugin runtime review around dependency diagnostics beyond resolver fallback and remaining version edge cases.

### 2026-05-07 08:04 AWST - Plugin loading/runtime invalid fallback assembly
- Area: Plugin loading/runtime (`PluginLoadContext` managed dependency fallback); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoadContext.cs`, `tests/XerahS.Tests/Helpers/PluginLoaderTests.cs`, `Directory.Build.props`.
- Findings: A malformed same-name DLL in the plugin directory could throw during fallback identity inspection instead of being rejected as an unusable fallback candidate.
- Status: Upstream/develop and origin/develop already contained; ShareX.ImageEditor develop/remotes verified current. Fixed fallback identity inspection to ignore unreadable/invalid candidate assemblies, added regression coverage, and bumped version `0.22.222` -> `0.22.223`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260507-075941.log`); tests passed 843 total (`/tmp/xerahs-hourly-sweep/test-20260507-080303.log`).
- Follow-up: continue plugin runtime review around dependency diagnostics beyond resolver fallback and remaining version edge cases.

### 2026-05-07 12:03 AWST - Capture pipeline DXGI scaled crop bounds

- Area: Capture pipeline / Windows DXGI crop rect scaling; files: `src/platform/XerahS.Platform.Windows/Capture/DxgiCropRectHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `Directory.Build.props`.
- Findings: DXGI region cropping translated virtual desktop coordinates directly to bitmap pixels, so DPI-scaled captures could crop the wrong offset/size and empty virtual bounds could divide the coordinate contract implicitly.
- Status: Fast-forwarded local `develop` to `origin/develop` (`8194d78c` docs refresh); upstream/develop already contained. ShareX.ImageEditor develop/remotes verified current at `360eeabe`; no parent pointer change. Fixed DXGI crop scaling and empty-bounds rejection; added regression coverage; bumped version `0.22.223` -> `0.22.224`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260507-115917.log`); tests passed 845 total (`/tmp/xerahs-hourly-sweep/test-20260507-120238.log`).
- Follow-up: Continue capture pipeline review around rotated display bounds and cursor/selection parity between modern and fallback capture paths.

### 2026-05-07 16:05 AWST - Capture pipeline DXGI cursor capability parity

- Area: Capture pipeline / Windows DXGI backend capabilities; files: `src/platform/XerahS.Platform.Windows/Capture/DxgiCaptureStrategy.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiCapabilitiesHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Findings: DXGI advertised cursor capture support even though the capture path does not read pointer metadata or composite the cursor into returned bitmaps.
- Status: Upstream/develop and origin/develop already contained; ShareX.ImageEditor develop/remotes verified current at `360eeabe`; no parent pointer change. Fixed DXGI capabilities to report cursor capture unsupported until composition exists; added regression coverage; bumped version `0.22.224` -> `0.22.225`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260507-160249.log`); tests passed 846 total (`/tmp/xerahs-hourly-sweep/test-20260507-160400.log`).
- Follow-up: Continue capture pipeline review around rotated display bounds and implementing real DXGI cursor metadata composition before re-enabling cursor support.

### 2026-05-07 20:09 AWST - Capture pipeline DXGI rotated region source boxes

- Area: Capture pipeline / Windows DXGI rotated region capture; files: `src/platform/XerahS.Platform.Windows/Capture/DxgiCaptureStrategy.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiRotationHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Status: Upstream/develop and origin/develop already contained; ShareX.ImageEditor develop/remotes verified current at `360eeabe`; no parent pointer change. Fixed DXGI region capture to map desktop-oriented regions into the unrotated duplication texture for 90/180/270 degree outputs, rotate the copied sub-bitmap back to desktop orientation, added source-box regression coverage, and bumped version `0.22.225` -> `0.22.226`.
- Follow-up: Continue capture pipeline review around real DXGI cursor metadata composition before re-enabling cursor capture support.

### 2026-05-07 23:55 AWST / completed 2026-05-08 00:04 AWST - Capture pipeline DXGI context replacement cleanup

- Area: Capture pipeline / Windows DXGI duplication context lifecycle; files: `src/platform/XerahS.Platform.Windows/Capture/DxgiCaptureStrategy.cs`, `src/platform/XerahS.Platform.Windows/Capture/DisposableContextDictionary.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Findings: Repeated DXGI monitor refreshes replaced existing per-monitor duplication contexts without disposing the old duplication/device resources.
- Status: Merged upstream/develop docs commits `62a11bbf` and `eb555a81`, keeping KovaForge's fuller 2026-05-06 blog conflict content; ShareX.ImageEditor develop/remotes verified current at `360eeabe`; no parent pointer change. Fixed context replacement disposal and initialization-failure cleanup; added regression coverage; bumped version `0.22.226` -> `0.22.227`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260508-000033.log`); tests passed 856 total (`/tmp/xerahs-hourly-sweep/test-20260508-000345.log`).
- Follow-up: Continue capture pipeline review around real DXGI cursor metadata composition before re-enabling cursor capture support.

### 2026-05-08 03:55 AWST / completed 2026-05-08 04:04 AWST - Capture pipeline DXGI cursor composition

- Area: Capture pipeline / Windows DXGI cursor capture; files: `src/platform/XerahS.Platform.Windows/Capture/DxgiCaptureStrategy.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiCursorCompositionHelper.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiCapabilitiesHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Findings: DXGI region capture accepted `RegionCaptureOptions.IncludeCursor` but ignored it, so callers could request cursor capture and still receive cursorless images.
- Status: Upstream/develop and origin/develop already contained; ShareX.ImageEditor develop/remotes verified current at `360eeabe`; no parent pointer change. Added best-effort DXGI cursor overlay composition for region captures, advertised cursor capability, added placement regression coverage, and bumped version `0.22.227` -> `0.22.228`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260508-035909.log`); tests passed 859 total (`/tmp/xerahs-hourly-sweep/test-20260508-040223.log`).
- Follow-up: Continue capture pipeline review around full-screen DXGI cursor parity and reducing the legacy `WindowsModernCaptureService` cursor-overlay duplication.

### 2026-05-08 07:55 AWST / completed 2026-05-08 08:10 AWST - Capture pipeline DXGI full-screen cursor overlay

- Area: Capture pipeline / Windows DXGI full-screen cursor capture and ImageEditor upstream test health; files: `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiCaptureStrategy.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `ShareX.ImageEditor`, `tests/XerahS.Tests/Editor/EditorContextMenuSmokeTests.cs`, `tests/XerahS.Tests/Editor/SchemaDrivenFilterCatalogTests.cs`, `Directory.Build.props`.
- Findings: Full-screen DXGI cursor composition bypassed the tested cursor-placement guard and both DXGI cursor overlay paths relied on implicit GDI bitmap transparency; upstream ImageEditor sync also exposed stale parent tests and JSON serialization of live `SKBitmap` image annotations.
- Status: Fast-forwarded upstream/develop through `e7296c54`, updated/pushed ShareX.ImageEditor develop to `417f584`, added explicit transparent cursor overlays plus full-screen placement gating, ignored `ImageAnnotation.ImageBitmap` during `.xann` JSON serialization, refreshed upstream-aligned editor tests, and bumped version `0.22.228` -> `0.22.229`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260508-080657.log`); tests passed 861 total (`/tmp/xerahs-hourly-sweep/test-20260508-081007.log`).
- Follow-up: Continue capture pipeline review around consolidating the remaining GDI cursor overlay implementation and multi-monitor cursor edge cases.

### 2026-05-08 11:55 AWST / completed 2026-05-08 12:07 AWST - Capture pipeline DXGI full-screen cursor bounds

- Area: Capture pipeline / Windows DXGI cursor overlay bounds; files: `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiCaptureStrategy.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiCursorCompositionHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `Directory.Build.props`.
- Findings: Full-screen DXGI cursor overlay used `_screenService.GetVirtualScreenBounds()` after assembling the bitmap from DXGI output bounds, so monitor churn or enumeration differences could offset/reject cursor drawing; region and full-screen overlays also duplicated the GDI/SKBitmap bridge.
- Status: Fast-forwarded origin/develop docs commits `6053ec6e` and `1610b143`; upstream/develop already contained; ShareX.ImageEditor verified clean on `develop` at `417f584`. Fixed full-screen cursor placement to use captured DXGI bounds, centralized cursor overlay composition, added negative/expanded DXGI bounds regression coverage, and bumped version `0.22.229` -> `0.22.230`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260508-120541.log`); tests passed 862 total (`/tmp/xerahs-hourly-sweep/test-20260508-120647.log`).
- Follow-up: Continue capture pipeline review around remaining GDI fallback cursor hide/restore behavior and DXGI multi-adapter frame acquisition edge cases.

### 2026-05-08 15:55 AWST / completed 2026-05-08 16:05 AWST - Capture pipeline DXGI frame acquisition retry

- Area: Capture pipeline / Windows DXGI multi-monitor frame acquisition; files: `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiFrameAcquisitionHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Findings: Full-screen DXGI capture only attempted `AcquireNextFrame` once per output, so a transient first timeout or success without a desktop resource could leave one monitor blank while region capture already retried.
- Status: Upstream/develop and origin/develop were already contained; ShareX.ImageEditor verified clean on `develop` at `417f584`. Added retry gating for unusable DXGI frames, release/dispose cleanup before retry, regression coverage, and bumped version `0.22.230` -> `0.22.231`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260508-160408.log`); tests passed 866 total (`/tmp/xerahs-hourly-sweep/test-20260508-160519.log`).
- Follow-up: Continue capture pipeline review around remaining GDI fallback cursor hide/restore behavior and DXGI adapter-loss recovery.

### 2026-05-08 23:55 AWST / completed 2026-05-09 00:10 AWST - Capture pipeline DXGI adapter-loss fallback

- Area: Capture pipeline / Windows DXGI full-screen fallback; files: `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiFrameAcquisitionHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop blog commits `844f147c` and `5100f787`, preserving KovaForge's fuller daily-blog context while incorporating upstream additions; ShareX.ImageEditor verified clean on `develop` at `417f584`. Fixed full-screen DXGI capture to return null and trigger the existing GDI fallback when no outputs duplicate or any expected output fails frame acquisition after retry, preventing black/partial captures from being reported as success. Added fallback-decision regression coverage and bumped version `0.22.231` -> `0.22.232`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 870 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260509-000604.log`, `/tmp/xerahs-hourly-sweep/test-20260509-000931.log`.
- Follow-up: Continue capture pipeline review around remaining GDI fallback cursor hide/restore behavior and monitor-output disposal paths during DXGI enumeration failures.

### 2026-05-09 03:55 AWST / completed 2026-05-09 04:08 AWST - Capture pipeline cursor replacement cleanup

- Area: Capture pipeline / Windows DXGI cursor hide/restore; files: `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs`, `src/platform/XerahS.Platform.Windows/Capture/CursorReplacementHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Status: Upstream/develop and origin/develop had no new commits beyond the local queued upstream blog merge/fix; ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`. Fixed failed cursor replacement cleanup so unsuccessful `SetSystemCursor` copies are destroyed and the capture path only restores/sleeps after at least one cursor is actually replaced. Added regression coverage and bumped version `0.22.232` -> `0.22.233`. Commit/push blocker: GitHub App auth timed out under a 45s guard and direct HTTPS push failed with missing username credentials; local `develop` remains ahead of `origin/develop`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 872 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260509-040628.log`, `/tmp/xerahs-hourly-sweep/test-20260509-040730.log`.
- Follow-up: Continue capture pipeline review around monitor-output disposal paths during DXGI enumeration/setup failures and finish pushing the queued local commits once GitHub App auth is available. Push logs: `/tmp/xerahs-hourly-sweep/push-20260509-040851.log`, `/tmp/xerahs-hourly-sweep/push-direct-20260509-040948.log`.

### 2026-05-09 07:55 AWST / completed 2026-05-09 08:11 AWST - Capture pipeline DXGI enumeration cleanup

- Area: Capture pipeline / Windows DXGI output enumeration cleanup; files: `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs`, `src/platform/XerahS.Platform.Windows/Capture/DxgiOutputEnumerationCleanupHelper.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Status: Fast-forwarded local `develop` to `origin/develop` commit `6ab2a45b`; upstream/develop already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed DXGI full-screen capture cleanup so enumerated outputs and unique adapters are disposed on early returns, invalid bounds, device-creation skips, and setup failures. Added regression coverage and bumped version `0.22.233` -> `0.22.234`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 873 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260509-080839.log`, `/tmp/xerahs-hourly-sweep/test-20260509-080934.log`.
- Follow-up: Continue capture pipeline review around DXGI full-screen resource ownership during partial duplication setup and frame acquisition failures.

### 2026-05-09 11:55 AWST / completed 2026-05-09 12:06 AWST - OpenClaw plugin exporter diagnostic redaction

- Area: OpenClaw native plugin export / generated runner diagnostics; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Fast-forwarded local `develop` through origin docs commits `9f8bd5bc`/`14d8a6d6` and upstream release/integration commits through `54cbce83`; ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed the exported OpenClaw plugin runner to redact stdout as well as stderr before formatting failed XerahS command diagnostics, preventing token-like stdout from leaking through plugin errors. Added exporter regression coverage and bumped version `0.22.236` -> `0.22.237`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 878 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260509-120238.log`, `/tmp/xerahs-hourly-sweep/test-20260509-120558.log`.
- Follow-up: Continue reviewing the OpenClaw plugin export templates around generated TypeScript SDK imports and command timeout/error handling.

### 2026-05-09 13:55 AWST / completed 2026-05-09 14:06 AWST - OpenClaw plugin exporter JSON parsing

- Area: OpenClaw native plugin export / generated runner JSON parsing; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop release-prep commit `d84cc957`; ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed the exported OpenClaw plugin runner to parse raw stdout for expected JSON while keeping redacted stdout/stderr for diagnostics and non-JSON output, preventing token-like JSON URL fields from being corrupted before parsing. Added exporter regression coverage and bumped version `0.22.237` -> `0.22.238`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 878 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260509-140301.log`, `/tmp/xerahs-hourly-sweep/test-20260509-140605.log`.
- Follow-up: Continue reviewing OpenClaw plugin export timeout handling around child process termination and generated TypeScript SDK imports.

### 2026-05-09 15:55 AWST / completed 2026-05-09 16:08 AWST - OpenClaw plugin exporter SDK imports

- Area: OpenClaw native plugin export / generated TypeScript SDK imports; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop commits `83b367ab`, `437b49b6`, `0ea08f80`, and `314700ee`; ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed generated OpenClaw tools to import `jsonResult` from the core plugin SDK instead of the web-search provider SDK, added exporter regression coverage, and bumped version `0.22.238` -> `0.22.239`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 880 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260509-160350.log`, `/tmp/xerahs-hourly-sweep/test-20260509-160654.log`.
- Follow-up: Continue reviewing OpenClaw plugin export timeout handling around child process termination.

### 2026-05-09 17:55 AWST / completed 2026-05-09 18:35 AWST - OpenClaw plugin exporter timeout termination

- Area: OpenClaw native plugin export / generated runner timeout handling; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained; ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed generated runner timeout handling so timed-out XerahS child processes are terminated with a grace-period escalation, settled once, and reported only after process close. Added exporter regression checks and bumped version `0.22.240` -> `0.22.241`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 880 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260509-183036.log`, `/tmp/xerahs-hourly-sweep/test-20260509-183401.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner process lifecycle around spawn errors and stream cleanup.

### 2026-05-09 21:55 AWST / completed 2026-05-09 22:34 AWST - OpenClaw plugin exporter stdin pipe errors

- Area: OpenClaw native plugin export / generated runner process lifecycle; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Local `develop` already contained upstream/develop commits `c7fe1211` and `b96648a4` via merge `a0c01705`; origin/develop was behind. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed generated runner stdin error handling so expected `EPIPE` from early child exit is ignored while real stdin/spawn errors reject once through shared cleanup. Added exporter regression checks and bumped version `0.22.241` -> `0.22.242`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 887 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260509-222959.log`, `/tmp/xerahs-hourly-sweep/test-20260509-223316.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner stream cleanup around stdout/stderr error paths and cancellation.

### 2026-05-09 23:55 AWST / completed 2026-05-10 00:35 AWST - OpenClaw plugin exporter stdout/stderr stream errors

- Area: OpenClaw native plugin export / generated runner stream cleanup; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop docs commits `3cc08971` and `e19a8c4d`; ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed generated runner stdout/stderr stream error handling so stream failures reject through shared cleanup instead of surfacing as unhandled plugin-host errors. Added exporter regression checks and bumped version `0.22.242` -> `0.22.243`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 887 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260510-003031.log`, `/tmp/xerahs-hourly-sweep/test-20260510-003348.log`.
- Follow-up: Continue reviewing OpenClaw plugin export cancellation behavior and generated runner cleanup after abort-like host shutdown.

### 2026-05-10 06:27 AWST / completed 2026-05-10 06:36 AWST - OpenClaw plugin exporter rejection cleanup

- Area: OpenClaw native plugin export / generated runner cancellation and rejection cleanup; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Fast-forwarded local `develop` to origin commits `562cf474` and `6bbd0220`; upstream/develop was already contained. ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; upstream image editor has divergent `d3ef805`, no parent pointer change. Fixed generated runner rejection paths so stream/spawn failures terminate the child process with the existing graceful kill plus SIGKILL escalation instead of leaving a live child after host-side rejection. Added exporter regression checks and bumped version `0.22.243` -> `0.22.244`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 892 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260510-063042.log`, `/tmp/xerahs-hourly-sweep/test-20260510-063357.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner abort/cancellation behavior around caller-initiated cancellation and post-rejection close events.

### 2026-05-10 08:27 AWST / completed 2026-05-10 08:35 AWST - OpenClaw plugin exporter caller cancellation

- Area: OpenClaw native plugin export / generated runner cancellation; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Local `develop` already matched `origin/develop` at `991450e8` and already contained `upstream/develop`; ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`, upstream still divergent at `d3ef805`, no parent pointer change. Fixed generated runner caller cancellation so pre-aborted calls reject before spawn and active abort signals terminate the child with cleanup. Added exporter regression checks and bumped version `0.22.244` -> `0.22.245`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 892 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260510-083130.log`, `/tmp/xerahs-hourly-sweep/test-20260510-083459.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner process lifecycle around SDK-provided abort propagation from tool handlers.

### 2026-05-10 10:27 AWST / completed 2026-05-10 10:35 AWST - OpenClaw plugin exporter tool abort propagation

- Area: OpenClaw native plugin export / generated tool handler cancellation; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Fast-forwarded local `develop` to origin docs commits `73d4011c` and `cc3b163f`, then merged upstream/develop commits `98ecb500` and `05f38ed3`; ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`, no parent pointer change. Fixed generated OpenClaw tools to accept the SDK-provided `AbortSignal` and pass it into every `runXerahS` invocation so host-side tool cancellation terminates the child command. Added exporter regression checks and bumped version `0.22.245` -> `0.22.246`.
- Build/Test: Release build passed with 0 warnings/errors; Release tests passed 892 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260510-103205.log`, `/tmp/xerahs-hourly-sweep/test-20260510-103516.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner process lifecycle around tool-result formatting and CLI command cancellation parity.

### 2026-05-10 12:27 AWST / completed 2026-05-10 12:34 AWST - OpenClaw plugin exporter CLI cancellation parity

- Area: OpenClaw native plugin export / generated CLI cancellation; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop docs commit `688a8d69` via `457a7f0a`; ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`, no parent pointer change. Fixed generated OpenClaw CLI commands to abort active XerahS child runs on SIGINT/SIGTERM and unregister process listeners after completion. Added exporter regression assertions and bumped version `0.22.246` -> `0.22.247`.
- Verification: `dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/errors; `dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 892 total tests. Logs: `/tmp/xerahs-hourly-sweep/build-20260510-123017.log`, `/tmp/xerahs-hourly-sweep/test-20260510-123334.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner/tool behavior around tool-result formatting and generated CLI exit-code handling after cancellation.

### 2026-05-10 18:27 AWST / completed 2026-05-10 18:34 AWST - OpenClaw plugin exporter CLI cancellation exit codes

- Area: OpenClaw native plugin export / generated CLI cancellation; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop commits through `eae26d65`; ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`, no parent pointer change. Fixed generated OpenClaw CLI cancellation to convert SIGINT/SIGTERM aborts into conventional exit codes 130/143 instead of surfacing a generic rejection path. Added exporter regression assertions and bumped version `0.22.247` -> `0.22.248`.
- Verification: `dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/errors; `dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 892 total tests. Logs: `/tmp/xerahs-hourly-sweep/build-20260510-183033.log`, `/tmp/xerahs-hourly-sweep/test-20260510-183346.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner/tool behavior around tool-result formatting and non-cancellation CLI failure reporting.

### 2026-05-10 20:33 AWST / completed 2026-05-10 20:41 AWST - OpenClaw plugin exporter signal failure reporting

- Area: OpenClaw native plugin export / generated runner non-cancellation failure reporting; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop commit `1296a1bc`; origin/develop already matched local before the merge. ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`, no parent pointer change. Fixed generated runner failure formatting to report signal-terminated child commands as `signal SIG...` instead of `exit code null`. Added exporter regression assertions and bumped version `0.22.248` -> `0.22.249`.
- Verification: `dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/errors; `dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 892 total tests. Logs: `/tmp/xerahs-hourly-sweep/build-20260510-203727.log`, `/tmp/xerahs-hourly-sweep/test-20260510-204039.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner/tool behavior around invalid JSON diagnostics and preserving useful, redacted failure context.

### 2026-05-10 22:33 AWST / completed 2026-05-10 22:45 AWST - OpenClaw plugin exporter invalid JSON diagnostics

- Area: OpenClaw native plugin export / generated runner diagnostics and Release build health; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `src/desktop/app/XerahS.App/XerahS.App.csproj`, `Directory.Build.props`.
- Status: Fast-forwarded local `develop` to upstream/develop through `d17d6fd0`; KovaForge origin had no new commits before sync. ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`, no parent pointer change. Fixed generated runner invalid JSON failures to include redacted stdout/stderr context, and fixed plain Release solution builds by making app self-contained packaging RID-specific only. Bumped version `0.22.253` -> `0.22.254`.
- Verification: `dotnet restore XerahS.sln` passed after upstream RID restore issue; `dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/errors; `dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 892 total tests. Logs: `/tmp/xerahs-hourly-sweep/restore-20260510-224104.log`, `/tmp/xerahs-hourly-sweep/build-20260510-224252.log`, `/tmp/xerahs-hourly-sweep/test-20260510-224406.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated runner/tool behavior around upload URL schema validation and tool-result error shape.

### 2026-05-11 00:33 AWST / completed 2026-05-11 00:40 AWST - OpenClaw plugin exporter upload URL validation

- Area: OpenClaw native plugin export / generated upload result validation; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop docs/version commits `071ab1db`, `4bbebbd8`, and `469d72eb`; KovaForge origin had no new commits before sync. ShareX.ImageEditor verified on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed generated upload tools to reject array-shaped results, malformed URLs, and non-HTTP(S) URL schemes before returning tool JSON. Added exporter regression assertions and bumped version `0.22.254` -> `0.22.255`.
- Verification: `dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/errors; `dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 892 total tests. Logs: `/tmp/xerahs-hourly-sweep/build-20260511-003642.log`, `/tmp/xerahs-hourly-sweep/test-20260511-003959.log`.
- Follow-up: Continue reviewing OpenClaw plugin export tool-result error shape and doctor/bootstrap result schemas.

### 2026-05-11 02:33 AWST / completed 2026-05-11 02:42 AWST - OpenClaw plugin exporter uploader report schemas

- Area: OpenClaw native plugin export / generated doctor and bootstrap uploader result schemas; files: `src/desktop/cli/XerahS.CLI/Commands/BootstrapCommand.cs`, `src/desktop/cli/XerahS.CLI/Services/CliUploaderBootstrapper.cs`, `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained; ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Added `bootstrap uploaders --json`, made generated doctor/bootstrap tools validate uploader report arrays plus `HasBlockingIssues`, added exporter regression assertions, and bumped version `0.22.255` -> `0.22.256`.
- Verification: `dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/errors; `dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 892 total tests. Logs: `/tmp/xerahs-hourly-sweep/build-20260511-023950.log`, `/tmp/xerahs-hourly-sweep/test-20260511-024145.log`.
- Follow-up: Continue reviewing OpenClaw plugin export CLI parity around bootstrap JSON output and generated CLI doctor/bootstrap commands.

### 2026-05-11 04:33 AWST / completed 2026-05-11 04:39 AWST - OpenClaw plugin exporter CLI bootstrap JSON parity

- Area: OpenClaw native plugin export / generated CLI bootstrap uploader command; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained; ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed generated `xerahs bootstrap-uploaders` CLI command to call `bootstrap uploaders --json` with JSON parsing enabled, matching the tool handler and doctor CLI parity. Added exporter regression assertion and bumped version `0.22.256` -> `0.22.257`.
- Verification: Release build passed with 0 warnings/errors; tests passed 892 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260511-043556.log`, `/tmp/xerahs-hourly-sweep/test-20260511-043916.log`.
- Follow-up: Continue reviewing generated OpenClaw CLI output parity around upload command option handling and result formatting.

### 2026-05-11 06:33 AWST / completed 2026-05-11 06:43 AWST - OpenClaw plugin exporter CLI text upload parity

- Area: OpenClaw native plugin export / generated CLI upload command parity; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop release/Flatpak commits `b6b459f1` and `2bf1ee37` via `22159abf`, preserving KovaForge version lineage; pushed reconciled `develop` to origin. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated OpenClaw CLI parity by adding `xerahs upload-text <text>` with `--name` handling and JSON output, added exporter regression assertions, and bumped version `0.22.257` -> `0.22.258`.
- Verification: Explicit restore passed after initial missing-assets build failure; Release build passed with 0 warnings/errors; tests passed 892 total. Logs: `/tmp/xerahs-hourly-sweep/restore-20260511-063915.log`, `/tmp/xerahs-hourly-sweep/build-20260511-063929.log`, `/tmp/xerahs-hourly-sweep/test-20260511-064247.log`.
- Follow-up: Continue reviewing generated OpenClaw CLI output parity around structured error/result formatting for failed upload commands.

### 2026-05-11 10:33 AWST / completed 2026-05-11 10:40 AWST - OpenClaw plugin exporter CLI JSON result parity

- Area: OpenClaw native plugin export / generated CLI JSON result formatting; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged KovaForge origin/develop commits `e93f2854`, `3b6cc545`, and `6c917115` via `a1294238`; upstream/develop already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated OpenClaw CLI JSON-mode output to print the parsed result object instead of redacted stdout, preserving upload URLs with token-like query keys. Added exporter regression assertion and bumped version `0.22.258` -> `0.22.259`.
- Verification: Release build passed with 0 warnings/errors; tests passed 892 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260511-103648.log`, `/tmp/xerahs-hourly-sweep/test-20260511-104002.log`.
- Follow-up: Continue reviewing generated OpenClaw CLI output parity around structured error formatting for failed upload commands.

### 2026-05-11 14:33 AWST / completed 2026-05-11 14:40 AWST - GDI fallback cursor replacement cleanup

- Area: Capture pipeline / Windows GDI fallback cursor hide/restore; files: `src/platform/XerahS.Platform.Windows/WindowsScreenCaptureService.cs`, `tests/XerahS.Tests/Platform/Windows/WindowsModernCaptureServiceTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed legacy GDI cursor hiding to use the shared cursor replacement helper so failed `SetSystemCursor` copies are destroyed and all-failed replacement attempts do not report success. Added regression coverage for zero cursor-copy handles and bumped version `0.22.259` -> `0.22.260`.
- Verification: Release build passed with 0 warnings/errors; tests passed 893 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260511-143650.log`, `/tmp/xerahs-hourly-sweep/test-20260511-144008.log`.
- Follow-up: Continue reviewing generated OpenClaw CLI output parity around structured error formatting for failed upload commands, and keep an eye on any remaining cursor hide/restore duplication between DXGI and GDI paths.

### 2026-05-11 16:33 AWST / completed 2026-05-11 16:41 AWST - OpenClaw generated CLI JSON validation parity

- Area: OpenClaw native plugin export / generated CLI structured JSON validation; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated CLI upload/doctor/bootstrap commands to validate upload URL objects and uploader reports before printing JSON success, matching tool handler behavior. Bumped version `0.22.260` -> `0.22.261`.
- Verification: Release build passed with 0 warnings/errors; tests passed 893 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260511-163653.log`, `/tmp/xerahs-hourly-sweep/test-20260511-164009.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated CLI error presentation around non-JSON command failures and validator-thrown failures.

### 2026-05-11 18:33 AWST / completed 2026-05-11 18:40 AWST - OpenClaw generated CLI error presentation

- Area: OpenClaw native plugin export / generated CLI failure handling; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated OpenClaw CLI non-cancellation failures so runner and validator-thrown errors print a concise stderr message and exit code 1 instead of bubbling to host stack handling. Bumped version `0.22.261` -> `0.22.262`.
- Verification: Release build passed with 0 warnings/errors; tests passed 893 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260511-183610.log`, `/tmp/xerahs-hourly-sweep/test-20260511-183928.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated CLI presentation around preserving structured JSON-mode failure details without leaking sensitive diagnostics.

### 2026-05-11 22:33 AWST / completed 2026-05-11 22:40 AWST - OpenClaw generated CLI JSON validation diagnostics

- Area: OpenClaw native plugin export / generated CLI JSON validation diagnostics; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated OpenClaw CLI validator failures to include value-safe JSON shape details while avoiding raw field values, and bumped version `0.22.262` -> `0.22.263`.
- Verification: Release build passed with 0 warnings/errors; tests passed 893 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260511-223621.log`, `/tmp/xerahs-hourly-sweep/test-20260511-223939.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated CLI presentation around nested JSON shape summaries and upload command parity.

### 2026-05-12 00:33 AWST / completed 2026-05-12 00:42 AWST - OpenClaw generated CLI text upload parity

- Area: OpenClaw native plugin export / generated CLI upload text and JSON shape diagnostics; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop commits `b6aa7d55` and `92aaa9bc` via `a76a87f0`, preserving KovaForge version lineage and the fuller May 11 blog draft. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated `xerahs upload-text` to send text through stdin with `upload --pipe` instead of exposing text in child process arguments, added bounded nested JSON shape summaries for validator failures, and bumped version `0.22.263` -> `0.22.264`.
- Verification: Release build passed with 0 warnings/errors; tests passed 893 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260512-003828.log`, `/tmp/xerahs-hourly-sweep/test-20260512-004152.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated CLI/file upload parity around filename handling and safe structured diagnostics.

### 2026-05-12 02:33 AWST / completed 2026-05-12 02:42 AWST - OpenClaw generated upload filename parity

- Area: OpenClaw native plugin export / generated upload filename handling; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated tool-side optional filename parsing to trim upload `name` values and ignore all-whitespace names, matching generated CLI behavior for file and text uploads. Bumped version `0.22.264` -> `0.22.265`.
- Verification: Release build passed with 0 warnings/errors; tests passed 893 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260512-023657.log`, `/tmp/xerahs-hourly-sweep/test-20260512-024034.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated upload path handling around required path normalization and safe structured diagnostics.

### 2026-05-12 04:33 AWST / completed 2026-05-12 04:40 AWST - OpenClaw generated upload path normalization

- Area: OpenClaw native plugin export / generated upload path handling; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated file-upload path handling to trim required paths before invoking `xerahs upload`, reject empty CLI file args, and keep text upload content untrimmed. Bumped version `0.22.265` -> `0.22.266`.
- Verification: Release build passed with 0 warnings/errors; tests passed 893 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260512-043646.log`, `/tmp/xerahs-hourly-sweep/test-20260512-044008.log`.
- Follow-up: Continue reviewing OpenClaw plugin export safe structured diagnostics and any remaining generated CLI/tool parity gaps.

### 2026-05-12 06:33 AWST / completed 2026-05-12 06:40 AWST - OpenClaw generated CLI JSON shape diagnostic bounds

- Area: OpenClaw native plugin export / generated CLI safe structured diagnostics; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop doc status commit `ef0af0bd` via `cd8185d7`; KovaForge origin had no new commits before sync. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated CLI JSON validation shape diagnostics to cap object summaries at 12 keys with an omitted-key count, preventing malformed wide JSON from flooding stderr. Bumped version `0.22.266` -> `0.22.267`.
- Verification: Release build passed with 0 warnings/errors; tests passed 893 total. Logs: `/tmp/xerahs-hourly-sweep/build-20260512-063637.log`, `/tmp/xerahs-hourly-sweep/test-20260512-064000.log`.
- Follow-up: Continue reviewing OpenClaw plugin export safe structured diagnostics and any remaining generated CLI/tool parity gaps around generated command stderr formatting.
