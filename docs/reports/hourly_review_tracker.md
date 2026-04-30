# XerahS Hourly Review Current Tracker

Purpose: compact human-readable companion to `docs/reports/hourly_review_state.json` for the recurring XerahS review.

Use `hourly_review_state.json` as the hot machine-readable source. The full historical ledger was preserved at `docs/reports/archive/hourly_review_tracker_2026-04-30.md`.

## Rules

- Read `docs/reports/hourly_review_state.json` first; use this file only for quick human context.
- Prefer the stalest high-priority area that is not blocked by a larger prerequisite.
- Record raw/noisy evidence as log paths only; do not paste full command output here.
- After each run, update the JSON area row, keep only a compact current summary here, and archive any long detail outside the hot files.

## Next Candidates

- MCP server
- CLI / command surface
- Uploader core / plugin routing
- Platform-specific services
- Region capture / window enumeration

## Current Coverage

| Area | Last Reviewed | Priority | Last Outcome | Follow-up |
|---|---|---|---|---|
| Capture pipeline | 2026-04-30 05:41 AWST | High | Fixed DXGI rectangle capture crop conversion so fractional coordinates are preserved outward and non-finite/huge finite values are rejected/clamped before integer casts. | Continue capture pipeline review around DXGI multi-monitor rotation/scaling edge cases and GDI fallback parity. |
| OCR | 2026-04-30 06:41 AWST | High | Fixed onboarding OCR language refresh so removed language options are unsubscribed before replacement, preventing stale options from mutating selected languages after refresh. | Continue OCR review around onboarding selected-language collection replacement/unsubscription and platform OCR language refresh edge cases. |
| Settings/configuration | 2026-04-30 07:41 AWST | High | Fixed uploader config saves so SettingsChanged observers are notified for destination/provider changes; added regression coverage. | Continue settings review around async save completion semantics and custom config backup paths. |
| Assistant local memory/privacy/history | 2026-04-30 14:54 AWST | High | Fixed history file-path matching to use macOS case-insensitive semantics, so assistant OCR cache and privacy lookups find canonical history rows on default macOS volumes. | Continue assistant review around symlink-equivalent history paths and OCR cache invalidation when capture files are moved or deleted. |
| Tests / test discoverability | 2026-04-30 08:41 AWST | High | Marked main NUnit test discovery packages as private test/build assets so Microsoft.NET.Test.Sdk and NUnit3TestAdapter do not flow as normal transitive assets; added guardrail coverage. | Continue tests review around discovery package asset metadata for Avalonia.Headless.NUnit/coverage collectors and cross-target test host behavior. |
| Editor integration | 2026-04-30 09:43 AWST | High | Fixed editor window direct-close handling so ShowEditorSessionAsync completes with null instead of leaving callers awaiting forever; added regression coverage. | Continue editor integration review around Save/Save As result propagation and multi-image send-to sequencing. |
| Uploader core / plugin routing | 2026-04-30 16:50 AWST | High | Fixed encrypted Amazon S3 destination export so mobile .xsdc files require a configured bucket instead of exporting an incomplete destination; added regression coverage. | Continue uploader routing review around stale default-instance IDs, case-insensitive instance/category lookups, and mobile destination config validation parity. |
| Plugin loading/runtime | 2026-04-29 04:11 AWST | High | Fixed plugin package installation so a missing plugins root is created before installing a .xsdp package instead of failing with DirectoryNotFoundException. |  |
| FTP uploader plugin | 2026-04-30 02:18 AWST | Medium | Fixed FTP/SFTP missing-directory retry parent-path handling for bare, nested, absolute, and root-file remote paths. | Next FTP pass can inspect URL generation for HttpHomePathAutoAddSubFolderPath with absolute SFTP subfolders and name-parser tokens. |
| Hotkeys/input | 2026-04-30 15:46 AWST | Medium | Fixed Linux X11 hotkey matching so registered shortcuts ignore Caps Lock/Num Lock but reject unrelated extra modifiers; added regression coverage. | Continue hotkeys/input review around Wayland portal fallback state transitions and platform parity for modifier normalization. |
| Imgur uploader plugin | 2026-04-29 07:11 AWST | Medium | Fixed Imgur Client ID normalization before config save, login URL generation, uploader creation, and explorer auth setup. |  |
| Media subsystem | 2026-04-30 21:45 AWST | High | Fixed combined video thumbnail generation so skipped unreadable source images no longer shift timestamps onto later loaded images. | Continue media review around TakeThumbnails FFmpeg timeout/exit-code handling and mixed-dimension combined thumbnail layout. |
| MCP server | 2026-04-29 11:15 AWST | Medium | Fixed annotation renderer parameter parsing so malformed/scalar MCP annotation inputs are coerced or ignored safely. | Continue MCP review around RunTaskAsync upload result distinction and broader annotation schema validation. |
| CLI / command surface | 2026-04-29 14:05 AWST | Medium | Fixed CLI upload to use the direct upload processor/bootstrap readiness path and added uploader doctor/bootstrap commands. | Continue CLI review around uploader bootstrap provider choices and JSON doctor output contract. |
| Notifications/toasts | 2026-04-30 03:15 AWST | Medium | Fixed zero-duration auto-hide toasts so they start fade immediately instead of remaining visible indefinitely. | Toast opacity/fade behavior now has explicit zero-duration coverage; rotate through older tracker items next. |
| File/path handling | 2026-04-30 04:36 AWST | High | Fixed SettingsBase weekly backup scheduling so weekly-only configurations create backup archives. | Continue rotating through older settings/file persistence edge cases; consider checking backup retention pruning interactions next. |
| Indexer subsystem | 2026-04-29 18:17 AWST | Medium | Fixed negative MaxDepthLevel handling so non-positive depth is treated as unlimited instead of suppressing root files/folders. | Continue indexer review around unauthorized/path-too-long enumeration parity and output file collision handling. |
| Platform-specific services | 2026-05-01 01:45 AWST | High | Fixed macOS clipboard osascript launching to pass scripts via ArgumentList, drain stdout/stderr, and time out hung helper processes; added regression coverage; bumped version `0.22.165` -> `0.22.166`. | Continue platform-specific review around AppleScript file-list edge cases, macOS clipboard helper error surfacing, and Linux/Windows clipboard parity. |
| Region capture / window enumeration | 2026-04-29 20:17 AWST | High | Fixed Sway Wayland rectangle parsing so malformed rect objects and overflowing right/bottom edges are rejected safely. | Continue region/window enumeration review around GNOME eval rect validation and X11 frame extent overflow/invalid metadata handling. |

## Recent Runs

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
