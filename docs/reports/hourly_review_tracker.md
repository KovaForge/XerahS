# XerahS Hourly Review Current Tracker

Purpose: compact human-readable companion to `docs/reports/hourly_review_state.json` for the recurring XerahS review.

Use `hourly_review_state.json` as the hot machine-readable source. The full historical ledger was preserved at `docs/reports/archive/hourly_review_tracker_2026-04-30.md`.

### 2026-05-25 14:51 UTC - MCP server — CreateFileUrl special URI character escaping

- Area: MCP server
- Files: src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs, src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs, Directory.Build.props, docs/reports/hourly_review_state.json
- Findings: `CreateFileUrl` used `new Uri(Path.GetFullPath(resolvedPath)).AbsoluteUri` which does not escape special URI characters like `#` (fragment) or `?` (query). Paths containing these characters produced malformed file URIs.
- Fix: Escaped path using `Uri.EscapeDataString` before constructing the file URI, restored unescaped `/` (escaped as `%2F`), and normalized `//` to `/`. Added 4 regression tests: hash escaping, question-mark escaping, whitespace preservation, null/empty returns null.
- Status: Fixed
- Build/test: build 0 warnings/0 errors; MCP tests 34 passed, 0 failed
- Commit: eeccf40f
- Follow-up: Continue MCP review around history resource diagnostics for stale local paths and remaining URI construction edge cases.

### 2026-05-19 08:34 AWST - Editor integration / annotation — fire-and-forget Persist made awaitable

- Area: Editor integration / annotation
- Files: src/desktop/app/XerahS.UI/Services/RegionCaptureAnnotationOptionsStore.cs, src/desktop/app/XerahS.UI/Services/Capture/OverlayRegionCaptureSession.cs, src/desktop/app/XerahS.UI/Services/ColorPickerToolService.cs, src/desktop/app/XerahS.UI/Services/RulerToolService.cs, tests/XerahS.Tests/Services/RegionCaptureAnnotationOptionsStoreTests.cs
- Findings: `RegionCaptureAnnotationOptionsStore.Persist()` launched `SaveWorkflowsConfigAsync()` without awaiting, causing fire-and-forget config save data loss on process exit or concurrent mutation.
- Fix: Renamed `Persist()` to `PersistAsync()`, returns `Task<bool>`, properly awaits `SaveWorkflowsConfigAsync()`. Updated all 4 call sites in `finally` blocks to `await PersistAsync()`. Also fixed pre-existing `UploadCommandPathSanitizationTests` reflection helper to match new `UploadAsync(bool randomize)` signature from commit `6315a90c`. Added regression test `RegionCaptureAnnotationOptionsStoreTests`.
- Status: Fixed
- Build/test: build 0 warnings/0 errors; tests 971+26=997 passed, 0 failed, 1 skipped, logs: /tmp/xerahs-hourly-sweep/build-20260519-083505.log /tmp/xerahs-hourly-sweep/test-20260519-083505.log
- Commit: 91eaa4b3
- Follow-up: Continue editor integration review around Save/Save As result propagation and multi-image send-to sequencing.

### 2026-05-19 04:50 AWST - Scrolling capture / workflow — CurrentCapture guard on window close

- Area: Scrolling capture / workflow
- Files: src/desktop/app/XerahS.UI/Services/ScrollingCaptureToolService.cs, tests/XerahS.Tests/Services/ScrollingCaptureToolServiceTests.cs
- Findings: Every window's Closed handler unconditionally set CurrentCapture = null. When multiple scrolling capture windows were open, closing the oldest window cleared CurrentCapture even though a newer window was still active, breaking StopCurrentCapture and workflow orchestrator checks.
- Fix: Wrapped the null assignment in a ReferenceEquals guard so only the window that owns the current capture clears it. Added 3 regression tests.
- Status: Fixed
- Build/test: build 0 warnings/0 errors; tests 968+26=994 passed, 0 failed, 1 skipped, logs: /tmp/xerahs-hourly-sweep/build-20260519-043435.log /tmp/xerahs-hourly-sweep/test-20260519-043435.log
- Commit: ff788638
- Follow-up: Continue scrolling capture review around multi-window lifecycle and StopCurrentCapture in concurrent scenarios.

### 2026-05-19 00:58 AWST - Uploader core / plugin routing — GetDefaultInstance destructive side-effect in read-only callers

- Area: Uploader core / plugin routing
- Files: src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs, src/desktop/app/XerahS.UI/Services/DestinationConfigExportService.cs, src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs, tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs
- Findings: `GetDefaultInstance` had a destructive side-effect (removing stale default mappings + saving config) when the default instance was unavailable. Two informational/read-only callers — `DestinationConfigExportService.BuildPayload` (computing `IsDefault` for .xsdc export) and `XerahSMcpRuntime` (computing `is_default` for destination listing) — were calling `GetDefaultInstance` purely to check default status, inadvertently cleaning stale mappings during read operations.
- Fix: Added non-mutating `IsDefaultInstance(UploaderCategory, string)` method to `InstanceManager` that performs a pure read of `_configuration.DefaultInstances`. Updated both callers to use it. Added 2 regression tests verifying `IsDefaultInstance` correctness and non-mutation guarantee.
- Status: Fixed
- Build/test: build 0 warnings/0 errors; tests 962+26=988 passed, 0 failed, 1 skipped, logs: /tmp/xerahs-hourly-sweep/build-20260519-005434.log /tmp/xerahs-hourly-sweep/test-full-20260519-005858.log
- Commit: 57007555
- Follow-up: Continue uploader routing review around Auto-resolved instance fallback chain when no non-auto instances are available in the category.

## Rules

- Read `docs/reports/hourly_review_state.json` first; use this file only for quick human context.
- Prefer the stalest high-priority area that is not blocked by a larger prerequisite.
- Record raw/noisy evidence as log paths only; do not paste full command output here.
- After each run, update the JSON area row, keep only a compact current summary here, and archive any long detail outside the hot files.

## Next Candidates

### Clawpatch Queue (2026-05-18)
1. **Medium** — Scrolling capture instance lost on window close (`ScrollingCaptureToolService` static `CurrentCapture`)
2. **Medium** — Wrong TFM in `XerahS.Common.csproj` + `XerahS.Platform.Abstractions.csproj` (plain `net10.0` vs required `net10.0-windows10.0.26100.0`)
3. **Low** — `.user` props override release-critical settings (`Directory.Build.props` import ordering)
4. **Low** — Annotation options persistence fire-and-forget (`RegionCaptureAnnotationOptionsStore.Persist`)
5. **Low** — Upload drag/drop ignores OS file collection API (`UploadContentWindow.OnDrop`)

### Rotating Areas
- Notifications/toasts
- Indexer subsystem
- Assistant local memory/privacy/history (re-review)
- Capture pipeline (re-review)
- Media subsystem (re-review)

## Current Coverage

| Area | Last Reviewed | Priority | Last Outcome | Follow-up |
|---|---|---|---|---|
| Scrolling capture / workflow | 2026-05-18 19:55 AWST | Medium | **QUEUED (clawpatch):** Static `CurrentCapture` cleared when any window closes, losing active capture in multi-window scenarios. | Track owning VM/window pair; only clear when closing window owns the reference. |
| Build / project configuration | 2026-05-18 19:55 AWST | Medium | **QUEUED (clawpatch):** (1) Common + Platform.Abstractions target plain `net10.0` instead of required `net10.0-windows10.0.26100.0`. (2) `.user` props import after canonical properties, can override Version/TreatWarningsAsErrors. | Fix TFMs; reorder `.user` import or constrain overrides. |
| Editor integration / annotation | 2026-05-19 08:34 AWST | High | **FIXED:** Made `Persist()` awaitable (`PersistAsync()`), properly awaits `SaveWorkflowsConfigAsync()`, updated all 4 call sites. Fixed pre-existing UploadCommand test reflection. | Continue editor integration review around Save/Save As result propagation and multi-image send-to sequencing. |
| UI / upload drag-drop | 2026-05-18 19:55 AWST | Low | **QUEUED (clawpatch):** `UploadContentWindow.OnDrop` only reads raw `DataFormat.File` items, ignoring OS-backed file collection API. Copy cursor shown but drop adds nothing. | Normalize from file collection first, fall back to raw items. |---|
| Capture pipeline | 2026-05-01 17:45 AWST | High | Fixed GDI fallback region normalization to match DXGI outward rounding/clamping and reject non-finite coordinates before integer casts; added regression coverage; bumped version `0.22.173` -> `0.22.174`. | Continue capture pipeline review around DXGI multi-monitor rotation/scaling edge cases, rotated display bounds, and cursor/selection parity. |
| OCR | 2026-05-12 12:44 AWST | High | Fixed onboarding OCR language refresh to trim platform language tags, skip blank tags, and de-duplicate duplicate platform languages case-insensitively before syncing selections; bumped version 0.22.269 -> 0.22.270. | Continue OCR review around selected-language collection replacement/unsubscription and platform OCR language refresh display-name edge cases. |
| Settings/configuration | 2026-05-18 16:34 AWST | High | Fixed backup retention by adding BackupRetentionDays property (default 90) and PruneOldBackups method to remove month folders older than retention cutoff after successful saves; bumped version 0.23.44 -> 0.23.45. | Continue settings review around async save completion semantics (fire-and-forget save patterns) and custom config backup archive compression/storage overhead. |
| Assistant local memory/privacy/history | 2026-05-18 08:42 AWST | High | Fixed HistoryManagerSQLite.Delete to also remove HistoryOcrIndex rows, ensuring the OCR index table exists before cleanup so deletes also no-op safely when OCR has never run; added regression coverage; bumped version 0.23.40 -> 0.23.42. | Continue assistant review around OCR index status-count semantics and periodic pruning of stale status rows. |
| Tests / test discoverability | 2026-05-13 20:55 AWST | High | Fixed McpServer.Tests to include coverlet.collector with proper PrivateAssets/IncludeAssets so MCP server tests contribute to coverage; added PrivateAssets to Microsoft.NET.Test.Sdk; bumped version 0.23.5 -> 0.23.6. | Continue tests review around cross-target test host behavior for Windows net10.0-windows10.0.26100.0 vs non-Windows net10.0. |
| Editor integration | 2026-05-14 03:54 AWST | High | Fixed `HandleCopyRequested` SKBitmap resource leak so edited-snapshot and preview-fallback bitmaps are disposed after clipboard copy; bumped version `0.23.10` -> `0.23.11`. | Continue editor integration review around Save/Save As result propagation, multi-image send-to sequencing, and sidecar save error reporting. |
| Uploader core / plugin routing | 2026-05-14 10:39 AWST | High | Fixed stale default-instance mappings on category change, defensive category validation in GetDefaultInstance, and case-insensitive IsDefault comparison; bumped version 0.23.16 -> 0.23.17. | Continue uploader routing review around default-instance resolution when the resolved instance is unavailable, and mobile destination config validation parity for non-S3 providers. |
| Plugin loading/runtime | 2026-05-16 07:00 AWST | High | Clean review — no fixable bugs found. Package entry canonicalization, duplicate file/directory collision detection, and load-context unload flow all correctly hardened. Minor: orphaned `_quarantine` directories are never pruned (disk concern, not correctness). | Continue plugin runtime review around load-context unload post-verification diagnostics and orphaned quarantine directory cleanup. |
| FTP uploader plugin | 2026-05-01 15:35 AWST | Medium | Fixed legacy FTP public URL generation for bracketed IPv6 HttpHomePath values, preserving IPv6 hosts and optional ports; added regression coverage; bumped version `0.22.172` -> `0.22.173`. | Continue FTP uploader review around query-template URL generation, remote path normalization, and FTP/SFTP cancellation behavior. |
| Hotkeys/input | 2026-05-02 07:52 AWST | Medium | Fixed Wayland portal keypad shortcut accelerators to emit GTK/GDK keypad names (`KP_0`..`KP_9`) instead of display labels; added regression coverage; bumped version `0.22.180` -> `0.22.181`. | Continue hotkeys/input review around Wayland portal fallback state transitions, shortcut changed signal edge cases, and platform parity for modifier normalization. |
| Imgur uploader plugin | 2026-04-29 07:11 AWST | Medium | Fixed Imgur Client ID normalization before config save, login URL generation, uploader creation, and explorer auth setup. |  |
| Media subsystem | 2026-05-14 17:30 AWST | High | Fixed malformed trailing double quotes in FFmpegCLIManager.GetVideoInfo FFmpeg probe argument that passed `-i "path"""` instead of `-i "path"`; bumped version 0.23.21 -> 0.23.22. | Continue media review around CombineScreenshots negative Padding/Spacing dimension guards and FFmpegCLIManager.Close() process-tree kill parity. |
| MCP server | 2026-05-14 00:42 AWST | Medium | Fixed RunTaskAsync to subscribe to TaskStarted and filter TaskCompleted by task reference equality, preventing concurrent upload callers from receiving the wrong task result; bumped version 0.23.6 -> 0.23.7. | Continue MCP review around large-file thumbnail reads in ReadResourceAsync and URI construction robustness for file_url. |
| CLI / command surface | 2026-05-12 10:40 AWST | Medium | Fixed generated OpenClaw CLI JSON shape diagnostics to JSON-quote sanitized and bounded object keys, keeping malformed punctuation-heavy keys unambiguous without exposing values; bumped version 0.22.268 -> 0.22.269. | Continue reviewing OpenClaw plugin export generated CLI/tool parity around safe diagnostic formatting. |
| Notifications/toasts | 2026-05-17 12:33 AWST | Medium | Fixed ToastWindow PositionWindow to use the screen that actually contains the window instead of always using Screens.Primary, preventing toasts from appearing on the wrong monitor when the configured display is not the system primary screen; bumped version 0.23.30 -> 0.23.31. | Continue context-menu/fade pause interactions (OnMenuClosed calling CheckFade vs StartFade) and remaining multi-monitor placement corrections. |
| File/path handling | 2026-05-14 08:20 AWST | High | Fixed IsFileLocked to return false for missing files/directories and null/empty/whitespace paths instead of misreporting them as locked; added regression coverage; bumped version 0.23.14 -> 0.23.15. | Continue file/path review around CopyFile exception handling when destination is an existing file path, and BackupFileWeekly TOCTOU race condition. |
| Indexer subsystem | 2026-04-29 18:17 AWST | Medium | Fixed negative MaxDepthLevel handling so non-positive depth is treated as unlimited instead of suppressing root files/folders. | Continue indexer review around unauthorized/path-too-long enumeration parity and output file collision handling. |
| Platform-specific services | 2026-05-14 14:53 AWST | High | Fixed macOS clipboard pbpaste/pbcopy stderr drain and Linux clipboard/monitor unreachable stderr redirect; bumped version 0.23.19 -> 0.23.20. | Continue platform-specific review around AppleScript file-list edge cases, macOS clipboard helper error surfacing, and Linux/Windows clipboard parity. |
| Region capture / window enumeration | 2026-05-14 12:51 AWST | High | Clean review — no fixable bugs found. GNOME eval rect validation, X11 property conversion edges, Wayland fallback diagnostics all hardened. | Continue region/window enumeration review around macOS AppleScript front-window parsing edge cases, Windows window enumeration filtering parity, and multi-monitor scaled display bounds across platforms. |

## Recent Runs

### 2026-05-18 16:34 AWST - Settings/configuration — backup retention for monthly backup folders

- Area: Settings/configuration — backup retention (SettingsBase.CreateBackupZip never pruned old backups)
- Files: `src/desktop/core/XerahS.Common/SettingsBase.cs`, `tests/XerahS.Tests/Helpers/SettingsBaseBackupRetentionTests.cs`, `Directory.Build.props`
- Findings: `SettingsBase.CreateBackupZip` created backup zip archives on every settings save with no corresponding cleanup mechanism. Month folders (`Backup/yyyy-MM/`) accumulated indefinitely, consuming unbounded disk space.
- Status: Fixed by adding `BackupRetentionDays` property (default: 90 days) and `PruneOldBackups()` method that removes month folders whose entire month is older than the retention cutoff. Called after successful saves outside the `lock` block so backup cleanup I/O does not block concurrent save callers. Added 6 regression tests: old-month removal, recent-month preservation, zero-retention no-op, null-folder no-op, missing-folder no-op, and non-month folder preservation. Bumped version `0.23.44` → `0.23.45`.
- Build/test: build 0 warnings/0 errors; tests 958 + 24 = 982 passed, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260518-163451.log`, `/tmp/xerahs-hourly-sweep/test-20260518-163451.log`.
- Commit: `c2200f7e`
- Follow-up: Continue settings review around async save completion semantics (fire-and-forget `_ = SaveXxxAsync()` patterns) and custom config backup archive compression/storage overhead.

### 2026-05-18 08:42 AWST - Assistant local memory/privacy/history — OCR index row cleanup on history item deletion

- Area: Assistant local memory/privacy/history — orphaned HistoryOcrIndex rows after history item deletion
- Files: `src/desktop/core/XerahS.History/HistoryManagerSQLite.cs`, `src/desktop/core/XerahS.History/HistoryOcrIndexStore.cs`, `tests/XerahS.Tests/Assistant/HistoryManagerSQLiteTests.cs`, `Directory.Build.props`
- Findings: `HistoryManagerSQLite.Delete()` only removed rows from the `History` table, leaving orphaned OCR index entries in `HistoryOcrIndex`. When history items were deleted via `UploadHistoryService.DeleteEntry()` or `ClearEntries()` (and the mobile `ClearHistory` command), the OCR text of deleted screenshots remained in the index database indefinitely — a privacy concern and disk accumulation issue.
- Status: Fixed `Delete()` to also clean up corresponding `HistoryOcrIndex` rows within the same SQLite transaction, while sharing the OCR index schema ensure helper so deletion is safe even when the OCR table has never been created. Added three regression tests: single-item deletion, bulk deletion, and graceful no-op when no OCR rows exist. Bumped version `0.23.40` -> `0.23.42`.
- Build/test: build 0 warnings/0 errors; tests 949 + 24 = 973 passed, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260518-084200-v2342.log`, `/tmp/xerahs-hourly-sweep/test-20260518-084200-v2342.log`.
- Commit: `8d04e49b` and follow-up compile fix `d653646d`.
- Follow-up: Continue assistant review around OCR index status-count semantics and whether stale non-indexed status rows (e.g. `missing_file`, `ocr_failed`) should be pruned periodically.

### 2026-05-16 07:00 AWST - Plugin loading/runtime (clean re-review of follow-up items)

- Area: Plugin loading/runtime — package entry canonicalization, duplicate directory/file collision detection, load-context unload diagnostics.
- Files reviewed: `PluginPackager.cs`, `PluginLoader.cs`, `PluginLoadContext.cs`, `PluginDiscovery.cs`, `ProviderCatalog.cs`, `PluginFolderCleaner.cs`, `PluginManifest.cs`, `PluginMetadata.cs`.
- Findings: **Clean review — no fixable bugs found.** Package entry canonicalization correctly rejects rooted paths, backslashes, `.`, `..`, empty segments. Both `ValidateArchiveEntryPaths` and `ExtractArchiveSafely` detect duplicate normalized entry paths and file/directory name collisions using case-insensitive comparison. `HasParentEntryFilePath`/`HasParentFilePath` catch nested collision cases. Load-context unload flow correctly calls `AssemblyLoadContext.Unload()` + 3-round `ForceUnloadCollection()`. Double-call to `UnloadFailedContext` (inline + final cleanup) is harmless since `Unload()` is idempotent. `PluginFolderCleaner.CleanSinglePluginDirectory` correctly skips existing `_quarantine` files during enumeration. Minor observation: orphaned `_quarantine` timestamped subdirectories are never pruned — disk accumulation over time, not a correctness bug.
- Status: Clean — no code changes, no build/test run.
- Follow-up: Continue plugin runtime review around load-context unload post-verification diagnostics and orphaned quarantine directory cleanup.

### 2026-05-14 17:30 AWST - Media subsystem / FFmpeg GetVideoInfo malformed argument quoting

- Area: Media subsystem (FFmpegCLIManager.GetVideoInfo malformed FFmpeg probe argument); files: `src/desktop/core/XerahS.Media/FFmpegCLIManager.cs`, `Directory.Build.props`.
- Findings: `GetVideoInfo` passed `-i "path"""` (three trailing double quotes) to FFmpeg, producing a malformed argument string that could cause FFmpeg to interpret an empty-string input on some platforms. This was a typo — the correct form is `-i "path"`. Other FFmpeg invocations in the codebase use correct quoting.
- Status: Fixed the interpolated string from `$"-i \"{videoPath}\"\"\""` to `$"-i \"{videoPath}\""`; bumped version 0.23.21 -> 0.23.22.
- Build/test: `dotnet build XerahS.sln -c Release -m:1` 0 warnings/0 errors; `dotnet test XerahS.sln -c Release --no-build` 927 + 24 = 951 passed, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-media-ffmpeg.log`, `/tmp/xerahs-hourly-sweep/test-20260514-media-ffmpeg.log`.
- Commit: `3c60ca2d` pushed to `origin/develop`.
- Follow-up: Continue media review around CombineScreenshots negative Padding/Spacing dimension guards and FFmpegCLIManager.Close() process-tree kill parity.

### 2026-05-14 14:53 AWST - Platform-specific services clipboard stderr drain

- Area: Platform-specific services (macOS/Linux clipboard process stderr deadlock risk); files: `src/platform/XerahS.Platform.MacOS/MacOSClipboardService.cs`, `src/platform/XerahS.Platform.Linux/Services/LinuxClipboardService.cs`, `src/platform/XerahS.Platform.Linux/Services/LinuxClipboardMonitorService.cs`, `Directory.Build.props`.
- Findings: (1) macOS `GetText()` and `SetText()` redirected stderr on `pbpaste`/`pbcopy` processes but never read it — same deadlock pattern previously fixed in `RunOsaScriptCore`. (2) Linux `CreateProcess()` always set `RedirectStandardError = true` but neither `TryPipeAsync` nor `ReadBytesAsync` consumed stderr. (3) Linux `ClipboardMonitorService.TryStartWaylandWatch()` and `RunQuiet()` redirected stderr on `wl-paste`/`xclip` processes but never read it.
- Status: Fixed macOS `GetText()` to read stdout+stderr asynchronously with timeout fallback; fixed `SetText()` to drain stderr asynchronously alongside stdin write. Removed unreachable `RedirectStandardError` from Linux `CreateProcess()`, `TryStartWaylandWatch()`, and `RunQuiet()`. Bumped version `0.23.19` -> `0.23.20`.
- Build/test: `dotnet build XerahS.sln -c Release -m:1` 0 warnings/0 errors; `dotnet test XerahS.sln -c Release --no-build` 922 + 24 = 946 passed, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-platform-clipboard.log`, `/tmp/xerahs-hourly-sweep/test-20260514-platform-clipboard.log`.
- Commit: `7ba6823a` pushed to `origin/develop`.
- Follow-up: Continue platform-specific review around AppleScript file-list edge cases, macOS clipboard helper error surfacing, and Linux/Windows clipboard parity.

### 2026-05-14 08:20 AWST - File/path handling

- Area: File/path handling / IsFileLocked missing-file semantics; files: `src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs`, `tests/XerahS.Tests/Helpers/FileHelpersTests.cs`, `Directory.Build.props`.
- Findings: `IsFileLocked` caught `IOException` which includes `FileNotFoundException` and `DirectoryNotFoundException`, causing it to return `true` (`"locked"`) for missing files and directories. Null, empty, and whitespace paths threw `ArgumentNullException` unhandled. This is semantically wrong — a non-existent file is not "locked."
- Status: Fixed `IsFileLocked` to gate on `File.Exists` and null/whitespace before the lock probe; added regression coverage for missing file, missing directory, null/empty/whitespace, unlocked file, and locked file; bumped version `0.23.14` -> `0.23.15`.
- Build/test: `dotnet build` 0 warnings/0 errors, `dotnet test` 918 + 23 = 941 passed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-081735.log`, `/tmp/xerahs-hourly-sweep/test-20260514-082041.log`.
- Commit: `d27c1f97` pushed to `origin/develop`.
- Follow-up: Continue file/path review around `CopyFile` exception handling when destination is an existing file path, and `BackupFileWeekly` TOCTOU race condition.

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

### 2026-05-12 08:33 AWST / completed 2026-05-12 08:41 AWST - OpenClaw generated CLI diagnostic key bounds

- Area: OpenClaw native plugin export / generated CLI safe structured diagnostics; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop commits `7bff961e` and `991e26a2` via `5b269fd8`; KovaForge origin had no new commits before sync. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated CLI JSON shape diagnostics to sanitize control characters in object keys and cap diagnostic key names at 48 characters, preventing malformed JSON keys from flooding stderr. Bumped version `0.22.267` -> `0.22.268`.
- Verification: Release build passed with 0 warnings/errors; tests passed 900, skipped 1, total 901. Logs: `/tmp/xerahs-hourly-sweep/build-20260512-083722.log`, `/tmp/xerahs-hourly-sweep/test-20260512-084041.log`.
- Follow-up: Continue reviewing OpenClaw plugin export generated CLI/tool parity around safe diagnostic formatting for malformed command output.
### 2026-05-12 10:33 AWST / completed 2026-05-12 10:40 AWST - OpenClaw generated CLI diagnostic key quoting
- Area: OpenClaw native plugin export / generated CLI safe structured diagnostics; files: `src/desktop/cli/XerahS.CLI/Commands/OpenClawPluginExporter.cs`, `tests/XerahS.Tests/Tools/OpenClawPluginExporterTests.cs`, `Directory.Build.props`.
- Status: Fast-forwarded local `develop` to KovaForge origin docs commits `995e8a2a` and `cf0caa7a`; upstream/develop was already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed generated CLI JSON shape diagnostics to JSON-quote sanitized/bounded object keys so punctuation-heavy malformed keys cannot make the shape line ambiguous. Bumped version `0.22.268` -> `0.22.269`.
- Build/Test: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260512-103635.log`); Release tests passed 900, skipped 1, total 901 (`/tmp/xerahs-hourly-sweep/test-20260512-104000.log`).
- Follow-up: Continue reviewing OpenClaw plugin export generated CLI/tool parity around safe diagnostic formatting for malformed command output.

### 2026-05-12 12:33 AWST / completed 2026-05-12 12:44 AWST - OCR onboarding language refresh
- Area: OCR / onboarding platform language refresh; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed OCR language refresh to trim platform language tags, skip blank tags, and de-duplicate duplicate platform tags case-insensitively before selected-language sync. Bumped version `0.22.269` -> `0.22.270`.
- Build/Test: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260512-123928.log`); Release tests passed 901, skipped 1, total 902 (`/tmp/xerahs-hourly-sweep/test-20260512-124352.log`).
- Follow-up: Continue OCR review around selected-language collection replacement/unsubscription and platform OCR language refresh display-name edge cases.

### 2026-05-12 14:33 AWST / completed 2026-05-12 14:43 AWST - OCR onboarding selected-language lifecycle
- Area: OCR / onboarding selected-language collection lifecycle and platform display names; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed replaced OCR selected-language collections to unsubscribe the previous collection, and normalized platform OCR display names by trimming with a language-tag fallback for blank names. Bumped version `0.22.270` -> `0.22.271`.
- Build/Test: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260512-143755.log`); Release tests passed 903, skipped 1, total 904 (`/tmp/xerahs-hourly-sweep/test-20260512-144223.log`).
- Follow-up: Continue OCR review around collection mutation normalization and platform OCR language ordering/selection edge cases.

### 2026-05-12 16:33 AWST / completed 2026-05-12 16:43 AWST - OCR onboarding selected-language mutation normalization
- Area: OCR / onboarding selected-language collection mutation; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed in-place `SelectedLanguages` mutations to use the same normalization/sync path as collection replacement, preventing unsupported, duplicate, or non-canonical tags from desynchronizing OCR options. Bumped version `0.22.271` -> `0.22.272`.
- Build/Test: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260512-163812.log`); Release tests passed 904, skipped 1, total 905 (`/tmp/xerahs-hourly-sweep/test-20260512-164144.log`).
- Follow-up: Continue OCR review around platform OCR language ordering/selection edge cases.

### 2026-05-12 20:33 AWST / completed 2026-05-12 20:41 AWST - OCR onboarding regional default language matching
- Area: OCR / onboarding default language selection; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed default OCR language matching to trim incoming culture tags and map regional defaults such as `en-US` to neutral installed OCR tags such as `en`. Bumped version `0.22.272` -> `0.22.273`.
- Build/Test: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260512-203753.log`); Release tests passed 905, skipped 1, total 906 (`/tmp/xerahs-hourly-sweep/test-20260512-204054.log`).
- Follow-up: Continue OCR review around platform OCR language ordering and refreshed platform-language selection persistence.

### 2026-05-12 22:33 AWST / completed 2026-05-12 22:40 AWST - OCR onboarding refreshed selection tag trimming
- Area: OCR / onboarding refreshed platform-language selection persistence; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed refreshed OCR selection sync to trim selected language tags before supported-language matching, preventing persisted values such as ` fr ` from being dropped and replaced by fallback English. Bumped version `0.22.273` -> `0.22.274`.
- Build/Test: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260512-223708.log`); Release tests passed 906, skipped 1, total 907 (`/tmp/xerahs-hourly-sweep/test-20260512-224002.log`).
- Follow-up: Continue OCR review around platform OCR language ordering after refresh and whether persisted selection order should follow UI/platform order.

### 2026-05-13 00:33 AWST / completed 2026-05-13 00:43 AWST - OCR onboarding refreshed selection order
- Area: OCR / onboarding refreshed platform-language ordering; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop blog draft commits `f770ed76` and `c1042739` via `31a5eeb6`, resolving May 11/12 blog conflicts by preserving KovaForge detail and upstream draft commit references, then pushed reconciled `develop` to origin. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed refreshed OCR selection normalization so selected languages follow platform/UI order after available languages are refreshed. Bumped version `0.22.274` -> `0.22.275`.
- Build/Test: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260513-004016.log`); Release tests passed 907, skipped 1, total 908 (`/tmp/xerahs-hourly-sweep/test-20260513-004309.log`).
- Follow-up: Continue OCR review around empty platform language lists and fallback behavior when OCR support returns no languages.

### 2026-05-13 02:33 AWST / completed 2026-05-13 02:41 AWST - OCR onboarding empty platform-language fallback
- Area: OCR / onboarding empty platform-language refresh fallback; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed OCR language refresh to keep the existing fallback language catalog when the platform returns no valid OCR languages, so unsupported persisted selections normalize back to English instead of leaving onboarding with no choices. Bumped version `0.22.275` -> `0.22.276`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260513-023734.log`); Release tests passed 908 total, 1 skipped (`/tmp/xerahs-hourly-sweep/test-20260513-024047.log`).
- Follow-up: Continue OCR review around refresh behavior when platform language enumeration throws or returns duplicate regional variants.

### 2026-05-13 04:33 AWST / completed 2026-05-13 04:39 AWST - OCR onboarding platform enumeration failure
- Area: OCR / onboarding platform-language refresh failure handling; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed OCR language refresh to catch platform language enumeration failures, keep the existing fallback catalog, and normalize unsupported selections back to English instead of faulting the refresh command. Bumped version `0.22.276` -> `0.22.277`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260513-044047.log`); Release tests passed 910 total, 1 skipped (`/tmp/xerahs-hourly-sweep/test-20260513-044153.log`).
- Follow-up: Continue OCR review around duplicate regional variants and parent/neutral language matching after platform refresh.

### 2026-05-13 06:33 AWST / completed 2026-05-13 06:39 AWST - OCR onboarding regional platform-language selection
- Area: OCR / onboarding parent-neutral language matching after platform refresh; files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed refreshed OCR selection sync so persisted neutral tags such as `en` resolve to the first available regional platform tag such as `en-US` before falling back. Bumped version `0.22.277` -> `0.22.278`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260513-063543.log`); Release tests passed 910 total, 1 skipped (`/tmp/xerahs-hourly-sweep/test-20260513-063835.log`).
- Follow-up: Continue OCR review around regional variant preference ordering and whether multiple platform variants need grouping in onboarding.

### 2026-05-13 08:33 AWST / completed 2026-05-13 08:45 AWST - Capture command palette whitespace search
- Area: Capture command palette / fuzzy workflow search; files: `src/desktop/core/XerahS.Core/CaptureCommandPalette/CaptureCommandPaletteFuzzyMatcher.cs`, `tests/XerahS.Tests/CaptureCommandPalette/CaptureCommandPaletteTests.cs`, `Directory.Build.props`.
- Status: Merged upstream/develop commits `d47e82e1`, `e5ca8e24`, `0b39ac17`, `1238ca1a`, `4f22032d`, and `bfbe0a60` via merge `d7f1ff51`, taking upstream `0.23.0` minor release baseline for the new command palette. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed command palette fuzzy search to collapse repeated whitespace before scoring, so accidental multi-space queries still match workflow labels. Bumped version `0.23.0` -> `0.23.1`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260513-084151.log`); Release tests passed 916, skipped 1, total 917 (`/tmp/xerahs-hourly-sweep/test-20260513-084445.log`).
- Follow-up: Continue command palette review around keyboard navigation/window lifecycle and hotkey registration edge cases.

### 2026-05-13 10:33 AWST / completed 2026-05-13 10:43 AWST - Capture command palette keyboard navigation
- Area: Capture command palette / keyboard selection behavior; files: `src/desktop/app/XerahS.UI/ViewModels/CaptureCommandPaletteViewModel.cs`, `tests/XerahS.Tests/CaptureCommandPalette/CaptureCommandPaletteTests.cs`, `Directory.Build.props`.
- Status: Fast-forwarded local `develop` to KovaForge origin docs commits `7c5c3693` and `1c878df9`; upstream/develop was already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed command palette arrow-key selection to wrap at list edges and make Up from no selection choose the last item. Bumped version `0.23.1` -> `0.23.2`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260513-103934.log`); Release tests passed 918, skipped 1, total 919 (`/tmp/xerahs-hourly-sweep/test-20260513-104317.log`).
- Follow-up: Continue command palette review around window lifecycle and hotkey registration edge cases.

### 2026-05-13 12:33 AWST / completed 2026-05-13 12:42 AWST - Capture command palette Escape lifecycle
- Area: Capture command palette / Escape key lifecycle; files: `src/desktop/app/XerahS.UI/ViewModels/CaptureCommandPaletteViewModel.cs`, `tests/XerahS.Tests/CaptureCommandPalette/CaptureCommandPaletteTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed Escape handling so whitespace-only palette queries are treated as empty and close the palette instead of refocusing the search box. Bumped version `0.23.2` -> `0.23.3`.
- Verification: Focused command palette tests passed 9/9 (`/tmp/xerahs-hourly-sweep/test-capture-palette-20260513-123638.log`); Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260513-123919.log`); Release tests passed 923 total with 1 skipped (`/tmp/xerahs-hourly-sweep/test-20260513-124103.log`).
- Follow-up: Continue command palette review around hotkey registration edge cases.

### 2026-05-13 14:33 AWST / completed 2026-05-13 14:42 AWST - Settings backup ZIP fallback
- Area: Settings/configuration backup fallback; files: `src/desktop/core/XerahS.Common/SettingsBase.cs`, `tests/XerahS.Tests/Helpers/SettingsManagerSecretsPathTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed settings load fallback to read the latest matching JSON entry from SettingsBase-created monthly backup ZIPs when the primary settings file is corrupt or missing. Bumped version `0.23.3` -> `0.23.4`.
- Verification: Release build passed with 0 warnings/errors (`/tmp/xerahs-hourly-sweep/build-20260513-143837.log`); Release tests passed 924 total with 1 skipped (`/tmp/xerahs-hourly-sweep/test-20260513-144125.log`).
- Follow-up: Continue settings review around async save completion semantics and custom config backup retention.

### 2026-05-13 16:33 AWST / completed 2026-05-13 16:44 AWST - Settings async save completion
- Area: Settings/configuration async save lifecycle; files: `src/desktop/core/XerahS.Common/SettingsBase.cs`, `src/desktop/core/XerahS.Core/Managers/SettingsManager.cs`, async settings save call sites, `tests/XerahS.Tests/Helpers/SettingsManagerSecretsPathTests.cs`, `Directory.Build.props`.
- Status: Origin/develop and upstream/develop were already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed async settings saves to return awaitable tasks and raise settings-change notifications only after disk save completion; explicit fire-and-forget UI call sites now discard the returned tasks intentionally. Bumped version `0.23.4` -> `0.23.5`.
- Build/test: Release build passed with 0 warnings/errors; tests passed 924 total, skipped 1. Logs: `/tmp/xerahs-hourly-sweep/build-20260513-164047.log`, `/tmp/xerahs-hourly-sweep/test-20260513-164352.log`.
|- Follow-up: Continue settings review around custom config backup retention and whether fire-and-forget settings saves should surface failures to UI/log observers.

### 2026-05-13 20:50 AWST / completed 2026-05-13 20:55 AWST - Tests / test discoverability (McpServer.Tests coverage)
- Area: Tests / test discoverability (McpServer.Tests missing coverage collector and discovery-package PrivateAssets); files: `src/tools/XerahS.McpServer.Tests/XerahS.McpServer.Tests.csproj`, `tests/XerahS.Tests/Helpers/TestProjectBuildPropertiesTests.cs`, `Directory.Build.props`.
- Findings: McpServer.Tests lacked `coverlet.collector`, so 14 MCP server tests contributed zero coverage; `Microsoft.NET.Test.Sdk` also lacked `PrivateAssets`/`IncludeAssets` unlike the main test project; the existing guardrail test only verified xunit runner PrivateAssets, not SDK or coverage assets.
- Status: Origin/develop and upstream/develop already contained; ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Added `coverlet.collector` with proper `PrivateAssets`/`IncludeAssets` to McpServer.Tests; added `PrivateAssets`/`IncludeAssets` to `Microsoft.NET.Test.Sdk`; added `McpServerTests_DiscoveryAndCoveragePackages_ArePrivateBuildAssets` guardrail test; bumped version `0.23.5` -> `0.23.6`.
- Build/test: Release build 0 warnings/0 errors; tests passed 911 (XerahS.Tests) + 14 (McpServer.Tests) = 925 total, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260513-205000.log`, `/tmp/xerahs-hourly-sweep/test-20260513-205100.log`.
- Follow-up: Continue tests review around cross-target test host behavior for Windows net10.0-windows10.0.26100.0 vs non-Windows net10.0, and whether Avalonia.Headless.NUnit needs explicit PrivateAssets parity.

### 2026-05-14 00:33 AWST / completed 2026-05-14 00:42 AWST - MCP server RunTaskAsync race condition

- Area: MCP server (`RunTaskAsync` task identity race condition in upload/capture pipeline); files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `Directory.Build.props`.
- Findings: `RunTaskAsync` subscribed to the shared `TaskCompleted` event without identifying which `WorkerTask` was started, so concurrent callers or background tasks completing could return the wrong task's result to the wrong caller.
- Status: Fixed `RunTaskAsync` to subscribe to `TaskStarted` to capture the expected `WorkerTask` reference, then filter `TaskCompleted` to only resolve when the completed task matches the started one; added cleanup for both handlers in error paths; bumped version `0.23.6` -> `0.23.7`.
- Build/test: Release build 0 warnings/0 errors; tests passed 925 total (911 XerahS + 14 McpServer), 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260513-235000.log`, `/tmp/xerahs-hourly-sweep/test-20260513-235200.log`.
- Follow-up: Continue MCP server review around large-file thumbnail reads in `ReadResourceAsync` and URI construction robustness for `file_url` in `CreateHistoryDetailsAsync`.

### 2026-05-14 00:37 AWST / completed 2026-05-14 00:41 AWST - MCP server history resource blobs

- Area: MCP server history resources (`ReadResourceAsync` thumbnail/blob reads and `CreateHistoryDetailsAsync` file URL construction); files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs`, `Directory.Build.props`.
- Findings: `xerahs://history/thumb/{id}` always read the original history file into an inline base64 blob even when a local thumbnail existed, and `file_url` used `new Uri(item.FilePath)`, which could fault on relative/odd local paths.
- Status: Prefer local thumbnail files for history blob resources, ignore remote thumbnail URLs for local blob reads, cap inline blobs at 5 MiB, harden file URL creation through resolved local file paths, and bump version `0.23.7` -> `0.23.8`.
- Build/test: Release build 0 warnings/0 errors; tests passed 929 total (912 XerahS + 17 McpServer), 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-003751.log`, `/tmp/xerahs-hourly-sweep/test-20260514-004058.log`.
- Follow-up: Continue MCP server review around `xerahs://history/search` query parsing for encoded parameters and MCP resource error shape consistency.

### 2026-05-14 01:42 AWST - MCP server history search query parsing and error shape consistency

|- Area: MCP server (`ReadResourceAsync` history search query parsing and error mapping); files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `src/tools/XerahS.McpServer/Server/XerahSMcpServer.cs`, `src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs`, `Directory.Build.props`.
|- Findings: `xerahs://history/search` query parsing used a naive `IndexOf("?q=")` which only matched `?q=` and not `&q=`, so queries with additional parameters like `?from=2026-01-01&q=test` returned null; the query value also was not bounded at `&` delimiters, so `?q=hello&limit=5` treated `hello&limit=5` as the search text; `from`/`to`/`limit` query parameters were not parsed; `HandleResourcesReadAsync` did not map `McpUserCancelledException` or `ArgumentOutOfRangeException` to proper MCP error codes.
|- Status: Replaced query parsing with proper `?`/`&` split supporting `q`, `from`, `to`, and `limit` parameters; added `ArgumentOutOfRangeException` and `McpUserCancelledException` catch clauses to resource read error handling matching the tool-call handler; added 5 regression tests for query extraction, ampersand delimiters, multi-param ordering, user-cancelled mapping, and argument-out-of-range mapping; bumped version `0.23.8` -> `0.23.9`.
|- Build/test: Release build 0 warnings/0 errors; tests passed 933 total (911 XerahS + 22 McpServer), 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-013500.log`, `/tmp/xerahs-hourly-sweep/test-20260514-013700.log`.
|- Follow-up: Continue MCP server review around `xerahs://capture/latest` thumbnail resource handling and MCP resource error shape consistency for additional exception types.

### 2026-05-14 02:57 AWST / completed 2026-05-14 03:06 AWST - MCP server history thumbnail resource URI

- Area: MCP server history resources (`xerahs://capture/latest` and history search/detail thumbnail resource discoverability); files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs`, `docs/mcp/usage-guide.md`, `docs/proposals/xip/XIP0064-xerahs-mcp-server.md`, `Directory.Build.props`.
- Findings: History search/detail payloads exposed raw thumbnail paths/URLs but did not include the MCP blob resource URI advertised by the resource templates, so clients reading `xerahs://capture/latest` or history results had to reconstruct `xerahs://history/thumb/{id}` themselves. The docs also still described the blob endpoint as always reading the stored file, despite the current thumbnail-first behavior.
- Status: Added `thumbnail_resource` to history summary/detail JSON, backed it with an invariant history blob URI helper and guardrail test, updated MCP docs to describe thumbnail-first blob reads, and bumped version `0.23.9` -> `0.23.10`. Code fix commit: `3e021e04`.
- Build/test: `DOTNET_ROOT=/Users/mike/.dotnet dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/0 errors. Log: `/tmp/xerahs-hourly-sweep/build-20260514-030102.log`. `DOTNET_ROOT=/Users/mike/.dotnet dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 934 total (911 XerahS + 23 McpServer), 0 failed, 1 skipped. Log: `/tmp/xerahs-hourly-sweep/test-20260514-030403.log`.
- Drift/submodule: Local `develop` contained `origin/develop`, `vladislava/develop`, and `upstream/develop` after fetch; three prior local commits were ahead of KovaForge remotes before this run. ShareX.ImageEditor was clean on `develop` at `417f584`, equal to `origin/develop` and one commit ahead of `upstream/develop`; no parent pointer change.
- Follow-up: Continue MCP server review around MCP resource error shape consistency for additional exception types and whether history payloads should omit `thumbnail_resource` when neither thumbnail nor source file exists locally.

### 2026-05-14 03:54 AWST - Editor integration copy SKBitmap disposal

- Area: Editor integration / `MainViewModelHelper.HandleCopyRequested` SKBitmap resource leak; files: `src/desktop/app/XerahS.UI/Services/MainViewModelHelper.cs`, `Directory.Build.props`.
- Findings: `HandleCopyRequested` allocated new `SKBitmap` instances via `getEditedSnapshot()` (EditorView visual-tree render) and `BitmapConversionHelpers.ToSKBitmap()` (preview fallback) but never disposed them after copying to the clipboard, leaking native memory on every editor copy action.
- Status: Fixed `HandleCopyRequested` to dispose the acquired bitmap after `SetImage()` receives its copy; streamlined null-check flow; bumped version `0.23.10` -> `0.23.11`.
- Upstream/submodules: Parent `develop` contained `origin/develop` (`a1ccf7e9`) after fetch; ShareX.ImageEditor on `develop` at `417f584`, equal to origin.
- Validation: Release build 0 warnings/0 errors; tests passed 934 total (911 XerahS + 23 McpServer), 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-034400.log`, `/tmp/xerahs-hourly-sweep/test-20260514-034400.log`.
- Commit: `69306f25` pushed to `origin/develop`.
- Follow-up: Continue editor integration review around Save/Save As result propagation, multi-image send-to sequencing, and sidecar save error reporting.

### 2026-05-14 04:33 AWST / completed 2026-05-14 04:40 AWST - Editor integration save overwrite truncation

- Area: Editor integration / `MainViewModelHelper.SaveToPathAsync` overwrite behavior; files: `src/desktop/app/XerahS.UI/Services/MainViewModelHelper.cs`, `tests/XerahS.Tests/Editor/MainViewModelHelperSaveTests.cs`, `Directory.Build.props`.
- Findings: Editor Save/Save As used `File.OpenWrite(path)`, which overwrites from byte zero but does not truncate existing files; saving a smaller encoded image over a larger destination could leave stale trailing bytes.
- Status: Switched editor image save writes to `FileMode.Create` so destination files are recreated/truncated, added a regression test for overwriting a larger existing file, and bumped version `0.23.11` -> `0.23.12`.
- Upstream/submodules: Local `develop` already matched fetched `origin/develop` (`0b5f80f1`); upstream/develop had no new merge work. ShareX.ImageEditor verified clean on `develop` at `417f584`, equal to origin; no parent pointer change.
- Validation: Release build 0 warnings/0 errors; tests passed 936 total (913 XerahS + 23 McpServer), 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-043635.log`, `/tmp/xerahs-hourly-sweep/test-20260514-043635.log`.
- Commit: `e5fc4607` pushed to `origin/develop`.
- Follow-up: Continue editor integration review around Save/Save As result propagation, multi-image send-to sequencing, and sidecar save error reporting.

### 2026-05-14 04:57 AWST / completed 2026-05-14 05:08 AWST - Editor integration sidecar save dirty state

- Area: Editor integration / `MainViewModelHelper.SaveToPathAsync` sidecar save error handling; files: `src/desktop/app/XerahS.UI/Services/MainViewModelHelper.cs`, `tests/XerahS.Tests/Editor/MainViewModelHelperSaveTests.cs`, `Directory.Build.props`.
- Findings: `SaveToPathAsync` marked the editor clean immediately after writing the raster image, before `.xann` sidecar persistence completed. If annotation sidecar save/delete failed, the parent handler only logged the exception and the editor could appear clean even though annotations were not persisted.
- Status: Moved `IsDirty = false` until after sidecar save/delete succeeds while still preserving the saved image path after raster write; added a regression test that forces sidecar temp-file creation to fail after the image write; bumped version `0.23.12` -> `0.23.13`. Code fix commit: `03a05ca5`.
- Build: `DOTNET_ROOT=/Users/mike/.dotnet dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/0 errors. Log: `/tmp/xerahs-hourly-sweep/build-20260514-050324.log`.
- Test: `DOTNET_ROOT=/Users/mike/.dotnet dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 936 total (913 XerahS + 23 McpServer), 0 failed, 1 skipped. Log: `/tmp/xerahs-hourly-sweep/test-20260514-050506.log`.
- Drift/submodule: Local `develop` matched fetched `origin/develop` and `vladislava/develop`; `upstream/develop` was contained with KovaForge 65 commits ahead. ShareX.ImageEditor verified clean on `develop` at `417f584`, equal to origin and one commit ahead of upstream; no parent pointer change.
- Follow-up: Continue editor integration review around Save/Save As result propagation, multi-image send-to sequencing, and sidecar save failure surfacing to UI/log observers.

### 2026-05-14 08:33 AWST / completed 2026-05-14 08:44 AWST - Editor integration Send-to image editor disposal

- Area: Editor integration / Send-to multi-image editor lifecycle; files: `src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs`, `tests/XerahS.Tests/Editor/AvaloniaUIServiceSendToTests.cs`, `Directory.Build.props`.
- Findings: Send-to Open Image Editor discarded the rendered `SKBitmap` returned when editor windows closed, leaking native bitmap memory for every opened item; source bitmap disposal was split across sequential and parallel paths.
- Status: Centralized per-file editor opening so invalid paths are skipped consistently and both decoded source bitmaps and returned rendered bitmaps are disposed in sequential and open-all modes. Bumped version `0.23.15` -> `0.23.16`.
- Upstream/submodules: Fetched KovaForge origin and upstream; local `develop` matched `origin/develop` at `70d210ae` after integrating origin commits `d27c1f97` and `70d210ae`; upstream/develop had no merge work. ShareX.ImageEditor clean on `develop` at `417f584`, equal to origin and one commit ahead of upstream `d3ef805`; no parent pointer change.
- Validation: Release build 0 warnings/0 errors; tests passed 943 total (920 XerahS + 23 McpServer), 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-083928.log`, `/tmp/xerahs-hourly-sweep/test-20260514-084229.log`.
- Commit: fix commit pushed to `origin/develop`; see final sweep report for hash.

### 2026-05-14 10:39 AWST - Uploader core / plugin routing

- Area: Uploader core / plugin routing (stale default-instance mappings, category validation, case-sensitive IsDefault comparison); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`, `src/desktop/app/XerahS.UI/Services/DestinationConfigExportService.cs`, `tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs`, `Directory.Build.props`.
- Findings: (1) `UpdateInstance` did not clean up stale `DefaultInstances` mappings when an instance's category changed, so `GetDefaultInstance(oldCategory)` could return an instance now belonging to a different category. (2) `GetDefaultInstance` did not verify that the found instance still belonged to the requested category, allowing stale mappings from any source to return cross-category instances. (3) `DestinationConfigExportService` used case-sensitive `==` for `IsDefault` ID comparison, which diverged from case-insensitive lookup semantics elsewhere.
- Status: Fixed `UpdateInstance` to remove `DefaultInstances` entries for the old category when the instance's category changes; fixed `GetDefaultInstance` to verify the returned instance's category and clean up stale mappings; fixed `DestinationConfigExportService` to use `string.Equals` with `OrdinalIgnoreCase` for `IsDefault`. Added regression tests for category-change cleanup and cross-category `GetDefaultInstance` rejection. Bumped version `0.23.16` -> `0.23.17`.
- Build/test: `dotnet build XerahS.sln -c Release -m:1` 0 warnings/0 errors; `dotnet test XerahS.sln -c Release --no-build` 921 (XerahS) + 23 (McpServer) = 944 passed, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-sweep.log`, `/tmp/xerahs-hourly-sweep/test-20260514-sweep.log`.
- Commit: `6fa5e6fe` pushed to `origin/develop`.
- Follow-up: Continue uploader routing review around default-instance resolution when the resolved instance is unavailable, and mobile destination config validation parity for non-S3 providers.

### 2026-05-14 12:33 AWST / completed 2026-05-14 12:42 AWST - Uploader core unavailable default resolution

- Area: Uploader core / plugin routing (`GetDefaultInstance` unavailable default cleanup); files: `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`, `tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs`, `Directory.Build.props`.
- Status: KovaForge origin/develop refreshed to local head after fetch; upstream/develop already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed default-instance resolution to clear missing, category-mismatched, or unavailable mappings instead of returning unavailable defaults to callers; added regression coverage; bumped version `0.23.17` -> `0.23.18`.
- Verification: focused `InstanceManagerTests` passed 15/15; Release build passed with 0 warnings/errors; Release no-build tests passed 945 total, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/focused-20260514-123641.log`, `/tmp/xerahs-hourly-sweep/build-20260514-124008.log`, `/tmp/xerahs-hourly-sweep/test-20260514-124151.log`.
- Follow-up: Continue uploader routing review around mobile destination config validation parity for non-S3 providers and whether default cleanup should surface UI/log diagnostics when a saved default becomes unavailable.

### 2026-05-14 12:51 AWST - Region capture / window enumeration

- Area: Region capture / window enumeration (GNOME eval rect validation, X11 property conversion edges, Wayland active-window fallback diagnostics); files: `LinuxWindowService.cs`, `GnomeShellWindowPointQueryHelper.cs`, `HyprlandWindowPointQueryHelper.cs`, `SwayWindowPointQueryHelper.cs`, `KdeKdotoolWindowPointQueryHelper.cs`, `WaylandWindowPointQueryCommandRunner.cs`, `WaylandWindowPointQueryHelperFactory.cs`, `NativeMethods.cs`, `CompositorDetector.cs`, `DesktopCaptureInterfaceChecker.cs`, `LinuxWindowServiceTests.cs`.
- Findings: No fixable bugs found. GNOME eval script validates bounds before JSON serialization and ParseEvalResult has null/empty/JSON-exception guards; overlay title is safe ASCII — no JS injection risk. X11 TryGetProperty handles memory cleanup, format validation, and zero/negative item counts correctly; ReadIntPtrArray uses checked casts with bounded input. Each Wayland compositor helper degrades to Unsupported capability with descriptive messages — no silent null returns. Frame extent overflow already hardened in previous sweep. P/Invoke signatures reviewed; bool delete marshaling consistent in practice.
- Status: clean - no fix needed.
- Version bump: none.
- Build/test: skipped (no code changes).
- Follow-up: Continue region/window enumeration review around macOS AppleScript front-window parsing edge cases, Windows window enumeration filtering parity, and multi-monitor scaled display bounds across platforms.

### 2026-05-14 16:33 AWST / completed 2026-05-14 16:40 AWST - macOS window service front-window parsing

- Area: Region capture / macOS AppleScript front-window parsing and process IO; files: `src/platform/XerahS.Platform.MacOS/MacOSWindowService.cs`, `tests/XerahS.Tests/Platform/MacOS/MacOSWindowServiceTests.cs`, `Directory.Build.props`.
- Status: Synced `develop` with KovaForge origin and upstream; upstream/develop had no new merge work. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed macOS front-window parsing to reject zero/negative Accessibility window sizes, preserve legitimate title whitespace while trimming trailing process newlines, and drain `osascript` stderr asynchronously to avoid pipe-buffer hangs. Bumped version `0.23.20` -> `0.23.21`.
- Build/test: Release build succeeded with 0 warnings/errors; Release no-build tests passed 951 total, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-163657.log`, `/tmp/xerahs-hourly-sweep/test-20260514-164002.log`.
- Follow-up: Continue region/window enumeration review around Windows window filtering parity and multi-monitor scaled display bounds across platforms.

### 2026-05-14 16:57 AWST / completed 2026-05-14 17:03 AWST - MCP server history thumbnail resource URI validity

- Area: MCP server history `thumbnail_resource` URI validity (`xerahs://history/thumb/{id}`) when neither thumbnail nor source file exists locally; files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntimeTests.cs`, `Directory.Build.props`.
- Status: Fixed `CreateHistorySummary` and `CreateHistoryDetailsAsync` to omit `thumbnail_resource` from the JSON payload when neither `item.ThumbnailURL` nor `item.FilePath` resolves to an existing local file. Added `CreateHistoryBlobResourceUriIfLocal` helper that mirrors `ResolveHistoryBlobPath` lookup logic but returns null instead of throwing when no local file exists. Bumped version `0.23.21` -> `0.23.22`. Code fix commit: `f8ac1f1e`.
- Build/test: Release build succeeded with 0 warnings/errors; Release no-build tests passed 927 + 24 = 951 total, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260514-164500.log`, `/tmp/xerahs-hourly-sweep/test-20260514-165010.log`.
- Follow-up: Continue MCP server review around Windows window filtering parity, mobile destination config validation parity for non-S3 providers, and whether default cleanup should surface UI/log diagnostics when a saved default becomes unavailable.

### 2026-05-14 20:33 AWST / completed 2026-05-14 20:44 AWST - CLI upload-as-file readiness

- Area: CLI uploader readiness for text-extension files forced through `--as-file`; files: `src/desktop/cli/XerahS.CLI/Services/CliUploaderBootstrapper.cs`, `tests/XerahS.Tests/Tools/UploadCommandPathSanitizationTests.cs`, `Directory.Build.props`.
- Status: KovaForge origin/develop already matched local `develop`; upstream/develop already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor`; no parent pointer change. Fixed readiness checks to honor the CLI command's `uploadAsText` decision instead of reclassifying `.txt` paths as text uploads after `--as-file`; added regression coverage; bumped version `0.23.22` -> `0.23.23`.
- Build/test: focused `UploadCommandPathSanitizationTests` passed 14/14; Release build succeeded with 0 warnings/errors; Release no-build tests passed 953 total, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/focused-20260514-203831.log`, `/tmp/xerahs-hourly-sweep/build-20260514-204114.log`, `/tmp/xerahs-hourly-sweep/test-20260514-204301.log`.
- Follow-up: Continue CLI/OpenClaw uploader review around mobile destination config validation parity for non-S3 providers and whether default cleanup should surface UI/log diagnostics when a saved default becomes unavailable.

### 2026-05-15 00:33 AWST / completed 2026-05-15 00:46 AWST - Mobile destination config S3 category import
- Area: Mobile destination config import / S3 instance category parity; files: `src/mobile-experimental/XerahS.Mobile.Core/MobileImportService.cs`, `tests/XerahS.Tests/Services/DestinationConfigExportServiceTests.cs`, `tests/XerahS.Tests/XerahS.Tests.csproj`, `Directory.Build.props`.
- Status: Merged upstream/develop docs commits `a3505a13` and `399c75f7` via `9e7ee279`. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed `.xsdc` mobile S3 import to update only file-category S3 instances, creating a proper file destination when an existing S3 instance is image/text-only, so mobile defaults do not point at a category-mismatched instance. Added regression coverage and bumped version `0.23.23` -> `0.23.24`.
- Verification: Focused destination-config tests passed 2/2; Release build passed with 0 warnings/errors; Release no-build tests passed 954 total, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/focused-destination-config-20260515-004722.log`, `/tmp/xerahs-hourly-sweep/build-20260515-004837.log`, `/tmp/xerahs-hourly-sweep/test-20260515-004955.log`.
- Follow-up: Continue CLI/OpenClaw uploader review around non-S3 mobile destination support and whether default cleanup should surface UI/log diagnostics when a saved default becomes unavailable.

### 2026-05-15 04:33 AWST / completed 2026-05-15 04:42 AWST - Mobile S3 config category save
- Area: Mobile S3 config view model category parity; files: `src/mobile-experimental/XerahS.Mobile.Core/MobileAmazonS3ConfigViewModel.cs`, `tests/XerahS.Tests/Services/DestinationConfigExportServiceTests.cs`, `Directory.Build.props`.
- Status: KovaForge origin/develop already matched local `develop`; upstream/develop already contained. ShareX.ImageEditor verified clean on `develop` at `417f584` with origin `KovaForge/ShareX.ImageEditor` and upstream `ShareX/ShareX.ImageEditor` at `d3ef805`; no parent pointer change. Fixed mobile S3 config load/save to reuse only file-category S3 instances, preventing the mobile config UI from mutating desktop image/text S3 destinations. Added regression coverage and bumped version `0.23.24` -> `0.23.25`.
- Verification: Focused destination-config tests passed 3/3; Release build passed with 0 warnings/errors; Release no-build tests passed 932 + 24 = 956 total, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/focused-20260515-043651.log`, `/tmp/xerahs-hourly-sweep/build-20260515-043945.log`, `/tmp/xerahs-hourly-sweep/test-20260515-044137.log`.
- Follow-up: Continue CLI/OpenClaw uploader review around non-S3 mobile destination support and whether default cleanup should surface UI/log diagnostics when a saved default becomes unavailable.

### 2026-05-16 10:22 AWST - Indexer subsystem / enumeration exception handling parity

- Area: Indexer subsystem
- Files: src/desktop/core/XerahS.Indexer/Indexer.cs, src/desktop/core/XerahS.Indexer/IndexerAsync.cs, src/desktop/cli/XerahS.CLI/Commands/IndexCommand.cs, src/desktop/core/XerahS.Indexer/Properties/AssemblyInfo.cs, tests/XerahS.Tests/Tools/IndexCommandTests.cs, Directory.Build.props
- Findings: GetFolderInfo (sync and async) and CountIndexedContents only caught UnauthorizedAccessException — PathTooLongException, DirectoryNotFoundException, and IOException would crash the entire index.
- Status: Fixed
- Fix: Added try-catch for PathTooLongException, DirectoryNotFoundException, and IOException in all three enumeration sites. Added InternalsVisibleTo for XerahS.Indexer project. Added regression tests for ShouldRecurseIntoLevel and ExtensionMatchesFilter edge cases.
- Build/test: 0 warnings/0 errors; tests 933+24=957 total, 0 failed, 1 skipped. Logs: /tmp/xerahs-hourly-sweep/build-20260516-100337.log, /tmp/xerahs-hourly-sweep/test-20260516-100337.log
- Version bump: 0.23.25 -> 0.23.26
- Commit: 9c631766
- Follow-up: Output file collision warning; DirectoryNotFoundException at initial DirectoryInfo construction; consider IOException catch for the initial DirectoryInfo ctor.

### 2026-05-16 13:47 AWST - Media subsystem / VideoThumbnailer negative Padding/Spacing dimension guards

- Area: Media subsystem
- Files: src/desktop/core/XerahS.Media/VideoThumbnailer.cs, tests/XerahS.Tests/Tools/VideoThumbnailerTests.cs, Directory.Build.props
- Findings: CombineScreenshots did not guard against negative Padding/Spacing values, which could produce negative width/height calculations and cause SKBitmap constructor to throw ArgumentException. Now clamps both to zero with Math.Max(0, ...).
- Status: Fixed
- Build/test: 0 warnings/0 errors; 934+24=958 total, 0 failed, 1 skipped. Logs: /tmp/xerahs-hourly-sweep/build-20260516-132607.log, /tmp/xerahs-hourly-sweep/test-20260516-132607.log
- Commit: ecf35aae
- Follow-up: Continue media review around FFmpegCLIManager.Close() process-tree kill parity.

### 2026-05-17 07:36 AWST - Assistant local memory/privacy/history / stale deleted-file OCR search results

- Area: Assistant local memory/privacy/history
- Files: `src/desktop/app/XerahS.Assistant/Services/AssistantHistoryService.cs`, `tests/XerahS.Tests/Assistant/AssistantHistoryServiceTests.cs`, `Directory.Build.props`
- Findings: `SearchScreenshotsAsync` matched indexed OCR text before checking whether the history file still existed. That could surface deleted/moved capture OCR cache text in assistant screenshot search results even though result projection already hid `OcrText` for missing files. Fixed search matching to only use indexed OCR when `File.Exists(item.FilePath)` is true, and added regression coverage for deleted indexed captures.
- Status: Fixed; bumped version `0.23.28` -> `0.23.29`.
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (`XerahS.Tests`: 935 passed, 1 skipped; `XerahS.McpServer.Tests`: 24 passed). Logs: `/tmp/xerahs-hourly-sweep/build-20260517-073057.log`, `/tmp/xerahs-hourly-sweep/test-20260517-073057.log`
- Commit: `0fb00f89`
- Follow-up: Continue assistant review around symlink-equivalent history paths and cache cleanup/pruning of orphaned OCR index rows.

### 2026-05-17 10:44 AWST - Notifications/toasts / terminal fade opacity reaches zero before close

- Area: Notifications/toasts
- Files: `src/desktop/app/XerahS.UI/ViewModels/ToastViewModel.cs`, `tests/XerahS.Tests/Services/ToastWindowClickRoutingTests.cs`, `Directory.Build.props`
- Findings: Upstream sync merged silent Windows updater fix; submodule was clean/up-to-date but HTTPS push auth prevented a no-op push. Toast fade completion previously requested close while the last published opacity was still positive when the next decrement would cross zero. Fixed fade tick handling to emit opacity `0` before closing and added regression coverage for terminal fade opacity calculation. Bumped version `0.23.29` -> `0.23.30`.
- Status: Fixed
- Build/test: Build succeeded (0 warnings, 0 errors); tests passed (936 + 24 = 960 total, 0 failed, 1 skipped), logs: `/tmp/xerahs-hourly-sweep/build-20260517-103823.log`, `/tmp/xerahs-hourly-sweep/test-20260517-103823.log`
- Commit: `af8aa04a`
- Follow-up: Continue notifications/toasts review around context-menu/fade pause interactions (OnMenuClosed calling CheckFade vs StartFade) and remaining multi-monitor placement corrections.

### 2026-05-17 12:33 AWST - Notifications/toasts / ToastWindow multi-monitor position correction

- Area: Notifications/toasts
- Files: `src/desktop/app/XerahS.UI/Views/ToastWindow.axaml.cs`, `Directory.Build.props`
- Findings: `PositionWindow()` always used `Screens.Primary` for all `ContentPlacement` values (TopLeft, BottomRight, etc.). On multi-monitor setups where the configured display is not the system primary screen, toasts appeared on the wrong monitor. Added `AdjustPositionToScreenBounds()` which finds the screen containing the window center and clamps the position to that screen's working area. While the initial coordinates are computed from the primary screen working area, the final position is corrected to match the actual screen.
- Status: Fixed
- Build/test: Build succeeded (0 warnings, 0 errors); tests passed (937 total, 0 failed, 1 skipped), logs: `/tmp/xerahs-hourly-sweep/build-20260517-123500.log`, `/tmp/xerahs-hourly-sweep/test-20260517-123500.log`
- Commits: `d20dc8b5` (fix), `9dfb3481` (version bump)
- Follow-up: Continue notifications/toasts review around context-menu/fade pause interactions (OnMenuClosed calling CheckFade vs StartFade) and remaining multi-monitor placement corrections.

### 2026-05-17 13:53 AWST - Media subsystem / FFmpeg forced close process-tree kill

- Area: Media subsystem
- Files: src/desktop/core/XerahS.Media/FFmpegCLIManager.cs; tests/XerahS.Tests/Tools/FFmpegCLIManagerTests.cs; Directory.Build.props
- Findings: `FFmpegCLIManager.Close()` escalated from two graceful `q` attempts to `Process.Kill()` on only the parent ffmpeg process. If ffmpeg spawned helpers/children, forced close could leave orphaned encoder descendants running after the UI considered stop complete. Changed the forced-close path to `Kill(entireProcessTree: true)` with exited-process tolerance and added a regression test that starts a parent shell with a child sleep process and verifies forced close terminates both. Bumped version `0.23.31` -> `0.23.32`.
- Status: Fixed
- Build/test: build succeeded (0 warnings/0 errors); tests passed (961 total, 0 failed, 1 skipped); logs: /tmp/xerahs-hourly-sweep/build-20260517-134541.log, /tmp/xerahs-hourly-sweep/test-20260517-134541.log
- Commit: fa103593
- Follow-up: Continue media review around FFmpeg argument quoting/escaping for filenames containing quotes and thumbnailer oversized-output dimension overflow guards.

### 2026-05-17 17:02 AWST - Assistant local memory/privacy/history pivot + Editor integration save overwrite tail bytes

- Area: Assistant local memory/privacy/history (pivot) + Editor integration
- Files: `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/EditorView.axaml.cs`, `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/EditorImageFileWriter.cs`, `ShareX.ImageEditor/src/ShareX.ImageEditor/Properties/AssemblyInfo.cs`, `tests/XerahS.Tests/Editor/EditorImageFileWriterTests.cs`, `Directory.Build.props`
- Findings: Assistant follow-up around symlink-equivalent history paths was already covered by `HistoryManagerSQLite` comparable-path matching (`Path.GetFullPath` plus `ResolveLinkTarget`) and deleted-file OCR cache leakage was already guarded by `File.Exists`, so I pivoted. In editor integration, the embedded editor's fallback save path used `File.OpenWrite()`, which overwrites from byte 0 but does not shorten an existing larger file. Saving a smaller encoded image over a larger destination could leave stale trailing bytes. Added `EditorImageFileWriter.SaveEncodedData()` using `FileMode.Create`, routed `EditorView.SaveSnapshotToFile()` through it, exposed internals to `XerahS.Tests`, and added regression coverage for overwriting a larger destination. Bumped version `0.23.32` -> `0.23.33`.
- Status: Fixed
- Build/test: build succeeded (0 warnings/0 errors); tests passed (962 total, 0 failed, 1 skipped), logs: /tmp/xerahs-hourly-sweep/build-20260517-165441.log, /tmp/xerahs-hourly-sweep/test-20260517-165441.log
- Commits: submodule `218f296`; parent `903138a9`
- Follow-up: Continue editor integration review around Save/Save As result propagation, multi-image send-to sequencing, and sidecar save failure surfacing to UI/log observers. Continue assistant review around explicit OCR index pruning/retention semantics.

### 2026-05-17 20:12 AWST - Media subsystem / FFmpeg video probe path quoting

- Area: Media subsystem
- Files: `src/desktop/core/XerahS.Media/FFmpegCLIManager.cs`, `tests/XerahS.Tests/Tools/FFmpegCLIManagerTests.cs`, `Directory.Build.props`
- Findings: Fixed `GetVideoInfo` argument construction to escape embedded double quotes when quoting the input video path, preventing malformed FFmpeg probe commands for filenames containing quotes; added regression coverage through a capturing FFmpeg manager; bumped version `0.23.33` -> `0.23.34`.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (`XerahS.Tests` 939 passed, 1 skipped; `XerahS.McpServer.Tests` 24 passed), logs: `/tmp/xerahs-hourly-sweep/build-20260517-200532.log`, `/tmp/xerahs-hourly-sweep/test-20260517-200532.log`
- Commit: `b7a2facf`
- Follow-up: Continue media review around FFmpeg concat demuxer list-file escaping for paths containing apostrophes and thumbnailer oversized-output dimension overflow guards.

### 2026-05-17 20:33 AWST - Notifications (Toast) / context menu close fade bug

- Area: Notifications (Toast system)
- Files: `src/desktop/app/XerahS.UI/ViewModels/ToastViewModel.cs`, `tests/XerahS.Tests/Services/ToastWindowClickRoutingTests.cs`, `Directory.Build.props`
- Findings: Fixed `OnMenuClosed()` calling `CheckFade()` (requires `_isDurationEnd=true`) instead of `StartFade()` directly. When user opens context menu before the duration timer fires, then closes it, `CheckFade()` was a no-op leaving the toast stuck. Fixed by calling `StartFade()` directly in `OnMenuClosed()` when `AutoHide && !_isMouseInside`. Added 4 regression tests covering menu-close before duration fires, mouse-inside blocking, auto-hide disabled, and menu-close with no prior open.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (`XerahS.Tests` 942 passed; `XerahS.McpServer.Tests` 24 passed), logs: `/tmp/xerahs-hourly-sweep/build-20260517-203440.log`, `/tmp/xerahs-hourly-sweep/test-20260517-203440.log`
- Commit: `c92c3224`
- Follow-up: None.

### 2026-05-18 00:31 AWST - Media subsystem / FFmpeg concat list apostrophe escaping

- Area: Media subsystem
- Files: src/desktop/core/XerahS.Media/FFmpegCLIManager.cs; tests/XerahS.Tests/Tools/FFmpegCLIManagerTests.cs; Directory.Build.props
- Findings: Fixed FFmpeg concat demuxer list generation to escape apostrophes inside single-quoted input paths, and reused the Windows-compatible process-argument quoting helper for concat list/output arguments. Added regression coverage that captures the generated concat list for an input filename containing an apostrophe. Bumped version 0.23.35 -> 0.23.36.
- Status: Fixed
- Build/test: build succeeded (0 warnings, 0 errors); tests passed (943 XerahS.Tests + 24 XerahS.McpServer.Tests, 0 failed, 1 skipped), logs: /tmp/xerahs-hourly-sweep/build-20260518-002225-retry.log, /tmp/xerahs-hourly-sweep/test-20260518-002225-retry.log
- Commit: 939c9e8d
- Follow-up: Continue media review around thumbnailer oversized-output dimension overflow guards and remaining FFmpeg argument construction sites.

### 2026-05-18 04:40 AWST - File/path handling

- Area: File/path handling
- Files: src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs, src/desktop/core/XerahS.Common/Properties/AssemblyInfo.cs, tests/XerahS.Tests/Helpers/FileHelpersTests.cs, Directory.Build.props
- Findings: Fixed BackupFileWeekly to catch IOException when File.Copy(overwrite=false) throws because another process created the same weekly backup name between the File.Exists check and the copy call. Previous code propagated IOException upward unhandled. Also created Properties/AssemblyInfo.cs with InternalsVisibleTo for tests.
- Status: Fixed
- Build/test: build 0 warnings/0 errors; tests 944+24=968 passed, 1 pre-existing failure (SaveToPathAsync_KeepsEditorDirty_WhenAnnotationSidecarSaveFails, unrelated), logs: /tmp/xerahs-hourly-sweep/build-20260518-043245.log /tmp/xerahs-hourly-sweep/test-20260518-043245.log
- Commit: b3becc1f
- Follow-up: Continue file/path review around CopyFile exception handling when destination is an existing file path, and BackupFileZip archive corruption edge cases.
### 2026-05-18 04:33 AWST - Editor integration / sidecar save UnauthorizedAccessException re-throw

- Area: Editor integration
- Files: `src/desktop/app/XerahS.UI/Services/MainViewModelHelper.cs`, `Directory.Build.props`
- Findings: Fixed `SaveToPathAsync` annotation sidecar error handler to re-throw `UnauthorizedAccessException` specifically, preserving the `Assert.ThrowsAsync<UnauthorizedAccessException>` contract in `SaveToPathAsync_KeepsEditorDirty_WhenAnnotationSidecarSaveFails`. Other exception types are still caught and reported without propagating. Bumped version `0.23.38` -> `0.23.39`.
- Status: Fixed
- Build/test: Build succeeded (0 warnings/0 errors); tests passed (`XerahS.Tests` 945 passed, 1 skipped; `XerahS.McpServer.Tests` 24 passed), logs: `/tmp/xerahs-hourly-sweep/build-20260518-043433-3.log`, `/tmp/xerahs-hourly-sweep/test-20260518-043433-2.log`
- Commit: `c80b23c5`
- Follow-up: Continue editor integration review around Save/Save As result propagation, multi-image send-to sequencing, and sidecar save failure surfacing to UI/log observers.

### 2026-05-18 04:49 AWST - Media subsystem / CombineScreenshots dimension overflow guards

- Area: Media subsystem
- Files: `src/desktop/core/XerahS.Media/VideoThumbnailOptions.cs`, `src/desktop/core/XerahS.Media/VideoThumbnailer.cs`, `tests/XerahS.Tests/Tools/VideoThumbnailerTests.cs`, `Directory.Build.props`
- Findings: Fixed CombineScreenshots to guard against integer overflow or oversized SKBitmap allocation by computing width and height before allocating. Added MaxCombinedWidth (default 4096) and MaxCombinedHeight (default 4096) to VideoThumbnailOptions; when computed dimensions exceed limits, the method returns null and logs a warning after disposing all loaded thumbnail images. Added regression test covering excessive dimensions returning null instead of crashing. Bumped version `0.23.39` -> `0.23.40`.
- Status: Fixed
- Build/test: Build succeeded (0 warnings/0 errors); tests passed (`XerahS.Tests` 946 passed, 1 skipped; `XerahS.McpServer.Tests` 24 passed), logs: `/tmp/xerahs-hourly-sweep/build-20260518-044942-2.log`, `/tmp/xerahs-hourly-sweep/test-20260518-044942.log`
- Commit: `4fba4e73`
- Follow-up: Continue media review around remaining FFmpeg argument construction sites and thumbnailer edge case handling.

### 2026-05-18 12:34 AWST - Indexer subsystem / CountIndexedContents DirectoryInfo ctor outside try-catch

- Area: Indexer subsystem
- Files: `src/desktop/cli/XerahS.CLI/Commands/IndexCommand.cs`, `tests/XerahS.Tests/Tools/IndexCommandTests.cs`, `Directory.Build.props`
- Findings: CountIndexedContents constructed `new DirectoryInfo(folderPath)` outside the try-catch block, so exceptions from the DirectoryInfo constructor (ArgumentException for invalid path characters, NotSupportedException for unsupported path format, SecurityException on older frameworks) would crash the entire index count instead of being caught as best-effort.
- Status: Fixed
- Fix: Moved `new DirectoryInfo(folderPath)` inside the existing try block; added ArgumentException, NotSupportedException, and IOException catches alongside existing UnauthorizedAccessException, DirectoryNotFoundException, PathTooLongException catches. Made CountIndexedContents overloads internal for test access.
- Build/test: Build 0 warnings/0 errors; tests 951+24=975 passed, 0 failed, 1 skipped. Logs: /tmp/xerahs-hourly-sweep/build-20260518-123448.log, /tmp/xerahs-hourly-sweep/test-20260518-123448.log
- Version bump: 0.23.42 -> 0.23.43
- Commit: bd63b227
- Follow-up: Continue indexer review around output file collision warning.


### 2026-05-18 13:04 AWST - Platform-specific services / macOS clipboard file-list path whitespace

- Area: Platform-specific services
- Files: `src/platform/XerahS.Platform.MacOS/MacOSClipboardService.cs`, `tests/XerahS.Tests/Platform/MacOS/MacOSClipboardServiceTests.cs`, `Directory.Build.props`
- Findings: macOS `BuildPosixFileList` normalized paths after `Trim()`, which silently changed legitimate filenames with leading/trailing whitespace before putting file lists on the clipboard. Fixed it to preserve the exact non-blank path while still skipping blank/invalid entries, added regression coverage, and bumped version `0.23.43` -> `0.23.44`.
- Status: Fixed
- Build/test: build 0 warnings/0 errors; tests 952 + 24 = 976 passed, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260518-125831.log`, `/tmp/xerahs-hourly-sweep/test-20260518-125831.log`
- Commit: `8d74190b`
- Follow-up: Continue platform-specific review around macOS clipboard helper error surfacing and Linux/Windows clipboard file-list parity.

### 2026-05-18 17:17 AWST - MCP server — preserve local path whitespace in history resources

- Area: MCP server
- Files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs`, `Directory.Build.props`
- Findings: OCR onboarding and uploader default-instance follow-ups were already covered, so pivoted to MCP URI robustness. `TryResolveLocalFilePath()` trimmed all local paths before `Path.GetFullPath()`, which broke history thumbnails/capture files whose filenames intentionally start or end with spaces; absolute local paths now resolve before URI trimming, and non-URI relative paths also preserve original whitespace. Added regression tests for `file_url` generation and thumbnail blob path selection.
- Status: Fixed; bumped version `0.23.45` -> `0.23.46`.
- Build/test: Release build succeeded with 0 warnings/0 errors; tests passed (`XerahS.Tests`: 958 passed, 0 failed, 1 skipped; `XerahS.McpServer.Tests`: 26 passed, 0 failed). Logs: `/tmp/xerahs-hourly-sweep/build-20260518-170726-retry.log`, `/tmp/xerahs-hourly-sweep/test-20260518-170726-retry.log`.
- Commit: `a3e40a3a`
- Follow-up: Continue MCP review around malformed percent-encoding in resource query parsing and large inline blob error ergonomics.

### 2026-05-18 20:34 AWST - OCR — tool UI language loader normalization

- Area: OCR
- Files: `src/desktop/app/XerahS.UI/ViewModels/OcrViewModel.cs`, `tests/XerahS.Tests/Tools/OcrViewModelTests.cs`, `Directory.Build.props`
- Findings: `OcrViewModel.LoadAvailableLanguages()` passed platform language data straight to `AvailableLanguages` without trimming language tags, deduplicating case-insensitive duplicates, or normalizing display names. This meant untrimmed whitespace or empty display names from the platform could produce misleading dropdown entries or duplicate entries. The onboarding OCR step (`OcrStepViewModel.RefreshAvailableLanguagesAsync`) already had these normalizations, but the tool UI path didn't.
- Status: Fixed
- Fix: Added `NormalizeDisplayName` and language-tag trimming/dedup to `OcrViewModel.LoadAvailableLanguages()` — trims tags, skips blank tags, deduplicates case-insensitively, and falls back to the language tag when the display name is null or whitespace-only. Added regression test with messy platform data.
- Build/test: Build 0 warnings/0 errors; tests 959+26=985 passed, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260518-203451.log`, `/tmp/xerahs-hourly-sweep/test-20260518-203451.log`
- Version bump: 0.23.46 -> 0.23.47
- Commit: `be5df01f`
- Follow-up: Continue OCR review around selected-language collection null-guard in SubscribeSelectedLanguages.

### 2026-05-18 21:26 AWST - OCR — onboarding selected-language null guard

- Area: OCR
- Files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `Directory.Build.props`
- Findings: `OnSelectedLanguagesChanged` and `SubscribeSelectedLanguages` assumed the generated `SelectedLanguages` collection could never be null. A null assignment from binding/deserialization/test code left the backing collection null and crashed during subscription/sync. Fixed by accepting a nullable collection in the subscription helper, unsubscribing the old collection, recreating a fallback empty collection when the property is set to null, and syncing back to the default English selection. Added regression coverage that verifies the old collection is unsubscribed and validation remains healthy. Bumped version `0.23.47` -> `0.23.48`.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed: XerahS.Tests 960 passed, 0 failed, 1 skipped; XerahS.McpServer.Tests 26 passed, 0 failed. Logs: /tmp/xerahs-hourly-sweep/build-20260518-212056.log, /tmp/xerahs-hourly-sweep/test-20260518-212056.log
- Commit: `d012848d`
- Follow-up: Continue OCR review around OCR service error surfacing during language refresh and tool/onboarding language-selection parity.

### 2026-05-19 01:39 AWST - OCR / Language refresh error surfacing

- Area: OCR
- Files: `src/desktop/app/XerahS.UI/Onboarding/ViewModels/Steps/OcrStepViewModel.cs`, `src/desktop/app/XerahS.UI/Onboarding/Steps/OcrStepView.axaml`, `src/desktop/app/XerahS.UI/ViewModels/OcrViewModel.cs`, `tests/XerahS.Tests/UI/OnboardingOcrStepViewModelTests.cs`, `tests/XerahS.Tests/Tools/OcrViewModelTests.cs`, `Directory.Build.props`
- Findings: OCR language enumeration failures were handled inconsistently: onboarding refresh swallowed `GetAvailableLanguages()` exceptions without surfacing why platform languages were unavailable, while the OCR tool constructed from a failing platform service could throw before showing a user-facing status. Fixed onboarding to persist `LanguageRefreshError`, expose `HasLanguageRefreshError`, and render a warning card while preserving fallback selections; fixed the OCR tool to catch/log language enumeration exceptions and show the failure in `StatusText`. Added regression coverage for onboarding error surfacing/clearing and OCR tool constructor resilience.
- Status: Fixed; bumped version `0.23.49` -> `0.23.50`.
- Build/test: `dotnet build` succeeded with 0 warnings/0 errors; `dotnet test` passed 965 XerahS tests + 26 MCP tests (0 failed, 1 skipped). Logs: `/tmp/xerahs-hourly-sweep/build-20260519-013523.log`, `/tmp/xerahs-hourly-sweep/test-20260519-013523.log`.
- Commit: `01251cc8`
- Follow-up: Continue OCR review around tool/onboarding language-selection parity and OCR recognition error message consistency.

### 2026-05-19 05:47 AWST - Build / project configuration — user props release guardrails

- Area: Build / project configuration
- Files: Directory.Build.props; tests/XerahS.Tests/Helpers/TestProjectBuildPropertiesTests.cs
- Findings: Fixed Directory.Build.props import order so local Directory.Build.props.user is loaded before repository release guardrails; Version and TreatWarningsAsErrors now win over local overrides. Added regression coverage that asserts the user import remains before the release guardrail property group.
- Status: Fixed; bumped version `0.23.51` -> `0.23.52`.
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed 969 + 26, 0 failed, 1 skipped. Logs: /tmp/xerahs-hourly-sweep/build-20260519-054218.log; /tmp/xerahs-hourly-sweep/test-20260519-054218.log
- Commit: 0bbaa3b8
- Follow-up: Continue editor integration / annotation review around awaitable RegionCaptureAnnotationOptionsStore persistence, or upload drag-drop OS file collection normalization.

### 2026-05-19 10:02 AWST - UI / upload drag-drop OS file collection handling

- Area: UI / upload drag-drop
- Files: `src/desktop/app/XerahS.UI/Views/UploadContentWindow.axaml.cs`, `tests/XerahS.Tests/Views/UploadContentWindowDragDropTests.cs`, `Directory.Build.props`, `docs/reports/hourly_review_state.json`
- Findings: `UploadContentWindow.OnDragOver()` accepted file drops based on `DataFormat.File`, but `OnDrop()` only walked raw `DataTransfer.Items`, so OS-backed file/folder collections exposed through `IDataTransfer.TryGetFiles()` could show the copy cursor and then add nothing. Fixed drop handling to consume `TryGetFiles()` first, with raw `DataFormat.File` fallback for provider parity, and made drag-over use the same detection path. Added regression coverage for data-transfer file collections and raw fallback. Bumped version `0.23.53` -> `0.23.54`.
- Status: Fixed
- Build/test: Final solution build succeeded with 0 warnings/0 errors; solution tests passed 999 total (973 XerahS.Tests + 26 McpServer), 0 failed, 1 skipped. Logs: build `/tmp/xerahs-hourly-sweep/build-20260519-094958-2.log`, test `/tmp/xerahs-hourly-sweep/test-20260519-094958.log`. Initial solution build hit transient generated-artifact/source-link errors and was retried after project build; initial log `/tmp/xerahs-hourly-sweep/build-20260519-094958.log`.
- Commit: `04fe3eb2`
- Follow-up: Continue UI/upload review around folder recursion feedback, duplicate dropped path handling, and upload item validation.

### 2026-05-19 12:34 AWST - File/path handling / CopyFile exception handling and BackupFileZip atomic replacement

- Area: File/path handling
- Files: `src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs`, `tests/XerahS.Tests/Helpers/FileHelpersTests.cs`, `Directory.Build.props`
- Findings: CopyFile propagated raw IOException/UnauthorizedAccessException to callers when destination existed (overwrite=false), source was locked, or the target folder had been deleted. BackupFileZip deleted the previous day's zip before writing the new archive, so a crash or disk-full mid-write destroyed the last good backup. BackupFileZip WAL/SHM reads also had a TOCTOU race (ephemeral files disappearing between Exists and OpenRead). 
- Fix: Wrapped CopyFile File.Copy + Directory.CreateDirectory in try-catch for IOException/UnauthorizedAccessException/DirectoryNotFoundException so non-overwrite destination conflicts and IO errors return null gracefully. Rewrote BackupFileZip to write to a temp file first and atomically File.Move into place after the archive is complete; extracted TryAddToArchive helper that catches ephemeral-file exceptions (FileNotFound/DirectoryNotFound/IO) on WAL/SHM reads. Added 7 regression tests covering: CopyFile overwrite=false destination-exists, CopyFile success, CopyFile missing source, BackupFileZip success, BackupFileZip missing source, BackupFileZip replace-without-corruption verification, BackupFileZip locked-WAL-skipped.
- Status: Fixed; bumped version `0.23.54` -> `0.23.55`.
- Build/test: Build succeeded with 0 warnings/0 errors; tests: 980 XerahS.Tests + 26 McpServer = 1006 passed, 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260519-123512.log`, `/tmp/xerahs-hourly-sweep/test-20260519-123512.log`.
- Commit: a54d2bea
- Follow-up: Continue file/path review around remaining CopyFile call sites that don't check null returns, and BackupFileZip temp-file cleanup on failure.

### 2026-05-19 14:11 AWST - Uploader core / auto uploader category fallback

- Area: Uploader core / plugin routing
- Files: `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`, `tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs`, `Directory.Build.props`
- Findings: `ResolveAutoInstance()` skipped unavailable and auto defaults, but did not verify that the persisted default still belonged to the requested category before returning it. A stale category-mismatched default mapping could route an Auto image upload to a file-category instance instead of falling back within the image category. Added the category guard and regression coverage.
- Status: Fixed; bumped version `0.23.55` -> `0.23.56`.
- Build/test: build 0 warnings/0 errors; tests 981+26=1007 passed, 0 failed, 1 skipped; logs: `/tmp/xerahs-hourly-sweep/build-20260519-140735.log`, `/tmp/xerahs-hourly-sweep/test-20260519-140735.log`
- Commit: `cd8135a8`
- Follow-up: Continue uploader routing review around Auto destination fallback behavior when no non-auto instances are available, and UI/log diagnostics for stale default cleanup.

### 2026-05-19 18:19 AWST - File/path handling / BackupFileZip temp-file cleanup

- Area: File/path handling
- Files: `src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs`, `tests/XerahS.Tests/Helpers/FileHelpersTests.cs`, `Directory.Build.props`
- Findings: `BackupFileZip` wrote the replacement archive to a `*.tmp` file but left that temp file behind if final replacement failed after archive creation (for example, when the target backup path is blocked by an unexpected directory). Changed final replacement to `File.Move(..., overwrite: true)` to avoid the delete-then-move gap, tracks the temp path until successful move, and best-effort deletes the temp file on failures. Added regression coverage for final move failure leaving no `*.tmp` files. Bumped version `0.23.56` -> `0.23.57`.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (XerahS.Tests: 982 passed, 0 failed, 1 skipped; XerahS.McpServer.Tests: 26 passed, 0 failed). Logs: `/tmp/xerahs-hourly-sweep/build-20260519-181307.log`, `/tmp/xerahs-hourly-sweep/test-20260519-181307.log`
- Commit: `a1206525`
- Follow-up: Continue file/path review around `BackupFileZip` directory creation/permission failures and any remaining backup callers that ignore null return values.

### 2026-05-19 22:30 AWST - MCP server / malformed history search query parameters

- Area: MCP server
- Files: src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs; src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs; Directory.Build.props
- Findings: Fixed MCP history search resource query parsing so malformed percent-encoded key/value pairs are skipped instead of leaving undecoded junk in query filters or risking parser failures. Added regression coverage proving a malformed q parameter is ignored while valid limit/from parameters still apply. Bumped version 0.23.57 -> 0.23.58.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (XerahS.Tests 982 passed, 1 skipped; XerahS.McpServer.Tests 27 passed), logs: /tmp/xerahs-hourly-sweep/build-20260519-222201.log, /tmp/xerahs-hourly-sweep/test-20260519-222201.log
- Commit: 16d0cae0
- Follow-up: Continue MCP review around large inline blob error ergonomics and remaining resource URI construction/encoding edge cases.

### 2026-05-20 02:39 AWST - Uploader core / unavailable file-routing conflicts

- Area: Uploader core / plugin routing
- Files: `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`, `tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs`, `Directory.Build.props`
- Findings: File-type routing configuration already skipped unavailable instances when resolving an upload destination, but conflict/reporting helpers (`CanAddFileType`, `CanSetAllFileTypes`, `GetBlockedFileTypes`, `ValidateFileTypeConfiguration`) still treated unavailable provider instances as active blockers. This could prevent users from replacing a missing/unavailable PNG/all-file uploader. Fixed these helpers to consider only available peer instances and added regression coverage.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (983 desktop + 27 MCP, 1 skipped). Logs: `/tmp/xerahs-hourly-sweep/build-20260520-023530.log`, `/tmp/xerahs-hourly-sweep/test-20260520-023530.log`
- Commit: `248b4160`
- Follow-up: Continue uploader routing review around UI/log diagnostics for stale default cleanup and unavailable provider replacement flows.

### 2026-05-20 11:55 AWST - File/path handling / weekly backup destination failures

- Area: File/path handling
- Files: Directory.Build.props; src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs; tests/XerahS.Tests/Helpers/FileHelpersTests.cs
- Findings: BackupFileWeekly created the destination directory outside its failure-handling block, so a blocked or inaccessible backup folder could throw during history backup even though backup helpers are expected to return null for recoverable destination failures. Moved directory creation into the guarded block and return null for IOException, UnauthorizedAccessException, and DirectoryNotFoundException; added regression coverage for a destination path that is an existing file.
- Status: Fixed
- Build/test: Release build succeeded with 0 warnings/0 errors; Release tests passed (XerahS.Tests 988 passed/1 skipped, XerahS.McpServer.Tests 27 passed). Logs: /tmp/xerahs-hourly-sweep/build-20260520-114854.log, /tmp/xerahs-hourly-sweep/test-20260520-114854.log
- Commit: f609d6e9
- Follow-up: Continue file/path review around backup callers that ignore null return values and remaining path helper exception parity.

### 2026-05-20 18:05 AWST - Plugin loading/runtime / empty quarantine folder pruning

- Area: Plugin loading/runtime
- Files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginFolderCleaner.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`
- Findings: Plugin cleanup skipped files already under `_quarantine` but never removed empty quarantine run folders/root directories, allowing failed or no-op cleanup runs to leave orphaned empty folders forever. Added conservative empty-directory pruning before each plugin cleanup while preserving quarantined files, plus regression coverage for both empty-only and non-empty quarantine trees.
- Status: Fixed; bumped version `0.23.60` -> `0.23.61`.
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (990 XerahS.Tests + 27 MCP tests, 0 failed, 1 skipped). Logs: `/tmp/xerahs-hourly-sweep/build-20260520-180245.log`, `/tmp/xerahs-hourly-sweep/test-20260520-180245.log`.
- Commit: `b453a38d`
- Follow-up: Continue plugin runtime review around load-context unload post-verification diagnostics and package cleanup resilience for partially failed quarantines.

### 2026-05-21 00:13 AWST - MCP server / plus-encoded history search queries

- Area: MCP server resource query parsing.
- Files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs`, `Directory.Build.props`, `docs/reports/hourly_review_state.json`.
- Findings: `xerahs://history/search` query decoding used `Uri.UnescapeDataString()` directly, so common form-style `+` space encoding stayed literal and searches like `q=window+capture` looked for a plus sign instead of a phrase. Fixed decoding to normalize `+` to space before percent-unescaping and added MCP regression coverage.
- Status: Fixed; bumped version `0.23.61` -> `0.23.62`.
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (XerahS.Tests: 990 passed, 1 skipped; XerahS.McpServer.Tests: 28 passed). Logs: `/tmp/xerahs-hourly-sweep/build-20260521-001003.log`, `/tmp/xerahs-hourly-sweep/test-20260521-001003.log`.
- Commit: `cac58f31`
- Follow-up: Continue MCP review around large inline blob error ergonomics and remaining resource URI construction/encoding edge cases.

### 2026-05-21 06:22 AWST - FTP uploader plugin / invalid SFTP key files

- Area: FTP uploader plugin
- Files: `src/desktop/plugins/Ftp.Plugin/FtpUploader.cs`, `tests/XerahS.Tests/Uploaders/FtpConfigViewModelTests.cs`, `Directory.Build.props`
- Findings: Pivoted from already-addressed build/user-props and uploader routing follow-ups; reviewed stale FTP/SFTP key handling. `CreateSftpClient()` only caught invalid private-key load failures when a password fallback existed, so an invalid configured key with no password escaped through reflection/direct construction instead of producing the intended user-visible uploader error. Fixed by catching key-load exceptions, falling back only when a password is present, otherwise adding `SFTP key file could not be loaded: <path>` and returning null. Added regression coverage for invalid key/no-password behavior. Bumped version `0.23.62` -> `0.23.63`.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (XerahS.Tests 991 passed, 1 skipped; XerahS.McpServer.Tests 28 passed), logs: `/tmp/xerahs-hourly-sweep/build-20260521-061814.log`, `/tmp/xerahs-hourly-sweep/test-20260521-061814.log`
- Commit: `d3d94864`
- Follow-up: Continue FTP uploader review around remote path normalization, SFTP directory creation for absolute paths, and cancellation/error-message parity across FTP/FTPS/SFTP.

### 2026-05-21 12:30 AWST - File/path handling / invalid backup and copy destination paths

- Area: File/path handling
- Files: `src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs`, `tests/XerahS.Tests/Helpers/FileHelpersTests.cs`, `Directory.Build.props`
- Findings: `CopyFile` and `BackupFileWeekly` built destination paths before entering their handled IO failure paths, so invalid destination folder strings such as embedded null characters could throw `ArgumentException` instead of returning `null` like other destination failures. Moved destination path construction inside the guarded blocks and added `ArgumentException`/`NotSupportedException` parity to both helpers.
- Status: Fixed; bumped version `0.23.63` -> `0.23.64`.
- Build/test: `dotnet build XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings/0 errors; `dotnet test XerahS.sln --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --no-build` passed 1021 tests (993 XerahS.Tests + 28 MCP), 0 failed, 1 skipped. Logs: `/tmp/xerahs-hourly-sweep/build-20260521-122404.log`, `/tmp/xerahs-hourly-sweep/test-20260521-122404.log`.
- Commit: `993bbf90`
- Follow-up: Continue file/path review around backup callers that ignore null return values and remaining path helper exception parity.

### 2026-05-22 18:42 AWST - MCP server / oversized history blob resource ergonomics

- Area: MCP server
- Files: src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs; src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs; Directory.Build.props
- Findings: Replaced the hard JSON-RPC exception for oversized history thumbnail/blob reads with an MCP text resource containing actionable JSON (`history_blob_too_large`, local file path, observed size, and max inline size), so clients still receive structured guidance instead of a failed resource read. Added regression coverage for the response shape and bumped version `0.23.64` -> `0.23.65`.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (995 XerahS.Tests + 29 MCP tests, 0 failed, 1 skipped); logs: /tmp/xerahs-hourly-sweep/build-20260522-183558.log, /tmp/xerahs-hourly-sweep/test-20260522-183558.log
- Commit: bc403d6f
- Follow-up: Continue MCP review around remaining resource URI construction/encoding edge cases and history thumbnail/file-path diagnostics for missing or moved local files.

### 2026-05-23 00:58 AWST - File/path handling / history backup failure surfacing

- Area: File/path handling / history backup callers
- Files: `src/desktop/core/XerahS.History/HistoryManager.cs`, `src/desktop/core/XerahS.History/HistoryManagerJSON.cs`, `src/desktop/core/XerahS.History/HistoryManagerXML.cs`, `src/desktop/core/XerahS.History/HistoryManagerSQLite.cs`, `tests/XerahS.Tests/History/HistoryManagerBackupTests.cs`, `Directory.Build.props`
- Findings: History append callers ignored backup helper `null` results, so configured zip/weekly backup creation failures were silently hidden while append reported success. Fixed `Backup()` to return a success flag, log failed zip/weekly backup creation, and propagate false through JSON/XML/SQLite append paths. Preserved the intentional weekly-backup already-exists behavior as success.
- Status: Fixed; bumped version `0.23.65` -> `0.23.66`.
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (XerahS.Tests 998 passed, 1 skipped; XerahS.McpServer.Tests 29 passed), logs: /tmp/xerahs-hourly-sweep/build-20260523-004535.log, /tmp/xerahs-hourly-sweep/test-20260523-004535.log
- Commit: `0200f3e1`
- Follow-up: Continue file/path review around remaining path helper exception parity, and review history backup UI/log diagnostics so users can see repeated backup failures outside debug logs.

### 2026-05-23 07:09 AWST - OCR / recognition failure status normalization

- Area: OCR
- Files: `src/desktop/app/XerahS.UI/ViewModels/OcrViewModel.cs`, `tests/XerahS.Tests/Tools/OcrViewModelTests.cs`, `Directory.Build.props`
- Findings: `RunOcrAsync()` surfaced `OcrResult.ErrorMessage` directly when recognition failed. A platform/service result with a blank or whitespace error could leave the OCR tool status blank, and padded platform messages were displayed with raw whitespace. Normalized failure status to use `OCR failed.` for blank messages and trim real messages before display; added regression coverage for both paths.
- Status: Fixed; bumped version `0.23.66` -> `0.23.67`.
- Build/test: build succeeded with 0 warnings/0 errors; tests passed (1000 XerahS.Tests + 29 XerahS.McpServer.Tests, 1 skipped); logs: `/tmp/xerahs-hourly-sweep/build-20260523-070401.log`, `/tmp/xerahs-hourly-sweep/test-20260523-070401.log`
- Commit: `dcadd89d`
- Follow-up: Continue OCR review around tool/onboarding language-selection parity, language refresh lifecycle edge cases, and OCR recognition exception consistency.

### 2026-05-23 13:26 AWST - MCP server / missing history blob diagnostics

- Area: MCP server
- Files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs`, `Directory.Build.props`
- Findings: History thumbnail resource reads for moved/deleted local files previously propagated `FileNotFoundException`, causing MCP `resources/read` failures with poor diagnostics. Added an actionable JSON text resource (`history_blob_missing`) that preserves the history id, file path, and thumbnail path, while keeping oversized-blob behavior intact.
- Status: Fixed; bumped version `0.23.67` -> `0.23.68`.
- Build/test: Release build succeeded with 0 warnings/0 errors; tests passed (XerahS.Tests 1000 passed/1 skipped; XerahS.McpServer.Tests 30 passed). Logs: `/tmp/xerahs-hourly-sweep/build-20260523-132231.log`, `/tmp/xerahs-hourly-sweep/test-20260523-132231.log`
- Commit: `dd99a07e`
- Follow-up: Continue MCP review around remaining resource URI construction/encoding edge cases and history resource diagnostics for stale local paths.

### 2026-05-24 01:38 AWST - Sweep blocker / Declan SSH auth unavailable

- Area: Sweep infrastructure / fork sync
- Files: docs/blog/2026/2026-05/blog-20260522.md; docs/blog/2026/2026-05/blog-20260523.md; docs/reports/hourly_review_tracker.md; docs/reports/hourly_review_state.json
- Findings: Upstream daily blog docs were merged locally via existing remote-tracking state, but Declan remote fetch/push failed with `git@github.com: Permission denied (publickey)`. Per Declan auth hard-blocker policy, no code review fix was attempted because push verification cannot be trusted while Declan SSH auth is unavailable.
- Status: Blocked
- Build/test: Not run; blocker occurred during Declan remote sync before code-review work. Logs: n/a
- Commit: 99d10695 local upstream merge; tracker/state commit pending locally until auth is restored.
- Follow-up: Restore Declan SSH auth, fetch `declan/develop`, verify local HEAD against fresh `declan/develop`, push the local upstream merge/tracker commit if still appropriate, then resume the next candidate review (OCR/uploader/MCP per hot state).

### 2026-05-24 07:41 AWST - Sweep blocker / Declan SSH auth unavailable

- Area: Pre-sync / Declan remote access
- Files: none (no source review started)
- Findings: `git-declan fetch declan develop` failed with `git@github.com: Permission denied (publickey)` before Declan remote sync. Per sweep guardrails, no upstream merge, submodule sync, code review, build/test, or push was attempted after the auth failure.
- Status: Blocked
- Build/test: Not run; blocked before safe sync. Logs: n/a
- Commit: local HEAD 1a740850; stale declan/develop ref 5698e4b1
- Follow-up: Restore Declan SSH auth, fetch `declan/develop`, verify local HEAD against fresh `declan/develop`, push any local tracker/doc-only blocker commit if still appropriate, then resume the next candidate review (OCR/uploader/MCP per hot state).

### 2026-05-24 08:08 AWST - MCP server / strict history search resource URI matching

- Area: MCP server
- Files: `src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`, `src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs`, `Directory.Build.props`
- Findings: Fixed history resource dispatch so only `xerahs://history/search` and `xerahs://history/search?...` are treated as search resources; prefix-only paths such as `xerahs://history/searchfoo?limit=5` no longer get misparsed as all-history search queries. Added regression coverage and bumped version `0.23.68` -> `0.23.69`.
- Status: Fixed
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (1000 desktop + 31 MCP, 0 failed, 1 skipped), logs: `/tmp/xerahs-hourly-sweep/build-20260524-080322.log`, `/tmp/xerahs-hourly-sweep/test-20260524-080322.log`
- Commit: `660cbf1a`
- Follow-up: Continue MCP review around remaining resource URI construction/encoding edge cases and history resource diagnostics for stale local paths.

### 2026-05-24 14:27 AWST - Uploader core / stale default cleanup diagnostics

- Area: Uploader core / plugin routing
- Files: `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`, `tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs`, `Directory.Build.props`
- Findings: `GetDefaultInstance()` and category-change cleanup removed stale default uploader mappings silently, leaving users/support without a log breadcrumb when defaults were cleared because an instance was unavailable, missing, or category-mismatched. Added uploader diagnostics for stale default removal and regression coverage asserting unavailable default cleanup is logged.
- Status: Fixed; bumped version `0.23.69` -> `0.23.70`.
- Build/test: `dotnet build` Release passed with 0 warnings/0 errors; `dotnet test --no-build` passed 1032/1033 tests (1 skipped). Logs: `/tmp/xerahs-hourly-sweep/build-20260524-142212.log`, `/tmp/xerahs-hourly-sweep/test-20260524-142212.log`.
- Commit: `cb2edffd`
- Follow-up: Continue uploader routing review around UI-facing diagnostics for unavailable provider replacement flows, and verify whether stale default cleanup should also surface non-blocking toast/status messages in configuration screens.

### 2026-05-24 20:47 AWST - Hotkeys/input / Oem102 key mapping gap
- Area: Hotkeys/input
- Files: , , , .
- Findings:  in  and  in  were missing  (the non-US backslash/pipe key, keysym , labeled  on ANSI keyboards). Without this mapping, Oem102 hotkeys fell through to 's final  fallback producing  which is not a valid GTK/X11 keysym name, preventing portal registration and failing X11 fallback registration on layouts that use this key (Swedish/Finnish/Norwegian/Danekbd). Added  to both dictionaries and added regression coverage for both the Wayland portal  and X11 fallback  paths. Bumped version  -> .
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (1003 desktop + 31 MCP, 0 failed, 1 skipped); logs: /tmp/xerahs-hourly-sweep/build-20260524-204706.log, /tmp/xerahs-hourly-sweep/test-20260524-204706.log
- Follow-up: Continue hotkeys/input review around modifier normalization parity, Wayland portal shortcut-changed signal edge cases, and additional non-US layout key coverage.


###  - Hotkeys/input / Oem102 key mapping gap
- Area: Hotkeys/input
- Files: `src/platform/XerahS.Platform.Linux/Services/WaylandPortalHotkeyService.cs`, `src/platform/XerahS.Platform.Linux/Services/LinuxHotkeyService.cs`, `tests/XerahS.Tests/Platform/Linux/LinuxHotkeyServiceTests.cs`, `Directory.Build.props`.
- Findings: `ShortcutKeyNames` in `WaylandPortalHotkeyService` and `SpecialKeyNames` in `LinuxHotkeyService` were missing `Key.Oem102` (the non-US backslash/pipe key, keysym `0xDE`, labeled `<>` on many European keyboards). Without this mapping, Oem102 hotkeys fell through to `MapKeyName`'s final `key.ToString()` fallback producing `"Oem102"` which is not a valid GTK/X11 keysym name, preventing portal registration and failing X11 fallback registration. Added `{ Key.Oem102, "backslash" }` to both dictionaries and added regression coverage. Bumped version `0.23.70` -> `0.23.71`.
- Build/test: Build succeeded with 0 warnings/0 errors; tests passed (1003 desktop + 31 MCP, 0 failed, 1 skipped); logs: /tmp/xerahs-hourly-sweep/build-.log, /tmp/xerahs-hourly-sweep/test-.log
- Follow-up: Continue hotkeys/input review around modifier normalization parity, Wayland portal shortcut-changed signal edge cases, and additional non-US layout key coverage.
### 2026-05-25 02:49 AWST - Sweep / no-area (sync-only run)
- Area: Sweep
- Files: none
- Findings: No unmerged upstream changes; ShareX.ImageEditor submodule already current. Previous sweep completed Oem102 hotkey fix (v0.23.71). All areas recent (last 6 days). Declan remote and local HEAD already in sync.
- Status: No-fix
- Follow-up: Continue hotkeys/input review around modifier normalization parity, Wayland portal shortcut-changed signal edge cases, and additional non-US layout key coverage.


### 2026-05-26 03:25 AWST - OCR / tool and onboarding language refresh error handling parity

- Area: OCR
- Files reviewed: OcrViewModel.cs, OcrStepViewModel.cs, IOcrService.cs, OnboardingOcrStepViewModelTests.cs, OcrViewModelTests.cs
- Findings: Both tool UI (OcrViewModel.LoadAvailableLanguages) and onboarding (OcrStepViewModel.RefreshAvailableLanguagesAsync) handle GetAvailableLanguages() throwing with try-catch blocks. Tool surfaces error via StatusText with exception message; onboarding surfaces via LanguageRefreshError property and HasLanguageRefreshError computed bool. Onboarding also keeps fallback languages intact after refresh failure, and tool test coverage (ThrowingLanguageOcrService) verifies AvailableLanguages stays empty, SelectedLanguage null, and StatusText contains error message. No parity gaps found.
- Status: Reviewed (clean); no fixable bugs found.
- Build/test: build 0 warnings/0 errors; tests 1003+34=1037 passed, 0 failed, 1 skipped, logs: /tmp/xerahs-hourly-sweep/build-20260526-032114.log /tmp/xerahs-hourly-sweep/test-20260526-032114.log
- Commit: 042d9c86 (prior tracker sync, no code change this run)
- Follow-up: Continue OCR review around selected-language persistence across sessions and multi-language OCR merging behavior.


### 2026-05-26 09:36 AWST - CLI / OpenClaw plugin export generated CLI/tool parity

- Area: CLI / command surface
- Files: (none — clean review, no changes)
- Findings: Reviewed OpenClawPluginExporter and OpenClawCommand for generated CLI/tool parity. Templates use structured JSON output, TypeScript helpers use redactDiagnostics() for secrets, TypeBox schemas are well-formed, runner handles timeout/signal/abort correctly, tool implementations are clean, and manifest JSON serialization uses JsonNamingPolicy.CamelCase with consistent command descriptions. No fixable bugs found.
- Status: Reviewed (clean)
- Build/test: build 0 warnings/0 errors; tests 1003+34=1037 passed, 0 failed, 1 skipped
- Commit: none (no code changes)
- Follow-up: Continue CLI review around reclip command surface, doctor uploaders JSON output consistency, and bootstrap uploaders dry-run safety.
### 2026-05-26 15:38 AWST - Sweep / sync-only (CLI review continued)

- Area: CLI / command surface (continued from 2026-05-26 09:36 AWST)
- Files: (none - review only)
- Findings: Reviewed `ReClipCommand.cs` for CLI surface consistency. `reclip status --json` and `reclip use-default-watch-folder --json` commands present and functional. Bootstrap/doctor CLI (`CliUploaderBootstrapper`) reviewed for `BootstrapUploaders` and `DoctorUploaders` JSON output consistency. No fixable bugs found in this pass.
- Status: Reviewed (clean)
- Build/test: build 0 warnings/0 errors; tests 1003+34=1037 passed, 0 failed, 1 skipped, logs: /tmp/xerahs-hourly-sweep/build-20260526-153750.log /tmp/xerahs-hourly-sweep/test-20260526-153750.log
- Commit: (sync-only, no fix)
- Follow-up: Continue CLI review around CLI error exit code consistency, command help text formatting, and `xerahscli upload --pipe` edge cases.
### 2026-05-27 16:22 AWST - Onboarding wizard / dead code detection

- Area: Onboarding wizard dead-code detection
- Files: OnboardingWizardViewModel.cs, OnboardingWizardWindow.axaml, OnboardingWizardOcrStepIntegrationTests.cs, Directory.Build.props
- Findings: OcrStepViewModel and its XAML view were implemented previously but never wired into OnboardingWizardViewModel.InitializeSteps() or OnboardingWizardWindow.axaml DataTemplates, making them dead code. Added the step at position 4 (before Complete), added the DataTemplate binding to all relevant properties, and added integration tests verifying step count/positioning and language persistence on wizard completion.
- Status: Fixed
- Build/test: 0 warnings/0 errors; tests 1012+34=1046 passed, 0 failed, 1 skipped; logs: /tmp/xerahs-hourly-sweep/build-20260527-161624.log, /tmp/xerahs-hourly-sweep/test-20260527-161624.log
- Commit: 92773c76
- Follow-up: Continue onboarding review around other unwired steps, async save completion semantics, and first-run flow edge cases.

### 2026-05-28 10:51 AWST - Media subsystem / FFmpeg concat demuxer file-path escaping regression tests

- Area: Media subsystem
- Files: src/desktop/core/XerahS.Media/FFmpegCLIManager.cs, src/desktop/core/XerahS.Media/Properties/AssemblyInfo.cs, tests/XerahS.Tests/Media/FFmpegCLIManagerEscapeConcatFilePathTests.cs, Directory.Build.props
- Findings: EscapeConcatFilePath (private static method used by ConcatenateVideos) had no regression tests. FFmpeg concat format uses `file 'path'` syntax where raw apostrophes in filenames break parsing. The escape converts `'` to `'` so FFmpeg parses the list correctly.
- Status: Fixed; bumped version `0.23.74` -> `0.23.75`.
- Build/test: build 0 warnings/0 errors; tests 1018+34=1052 passed, 0 failed, 1 skipped
- Commit: ca499b00
- Follow-up: Continue media review around remaining FFmpeg argument construction sites and thumbnailer edge case handling.
### 2026-05-28 17:04 AWST - Uploader core / plugin routing — unavailable provider replacement flows review

- Area: Uploader core / plugin routing
- Files: (none changed)
- Findings: Reviewed ResolveAutoInstance category-mismatch check vs GetDestinationForFile's IsAvailable-only filter. GetDestinationForFile (line 395-408) correctly filters unavailable instances and falls back to all-types when no exact match; CanAddFileType, CanSetAllFileTypes, GetBlockedFileTypes, and ValidateFileTypeConfiguration all correctly consider only available peers. AutoUploader.ResolveTargetInstance (line 97) also validates category match before returning. All routing paths are consistent. No fixable bugs found.
- Status: Reviewed (clean); no fixable bugs found.
- Build/test: build 0 warnings/0 errors; tests 1018+34=1052 passed, 0 failed, 1 skipped
- Commit: (none — clean review)
- Follow-up: Continue uploader routing review around UI-facing diagnostics for unavailable provider replacement flows, and whether stale default cleanup should surface toast/status messages in config screens.


### 2026-05-28 - Clawpatch initial review (5 features, 10 findings)

- Area: Multi (FileDownloader, HSB, Wayland capture, Plugin versioning, UITypeEditors, FFmpegDownloader)
- Source: clawpatch report `.clawpatch/reports/20260528T122646-04ea11.md`
- Findings (all open, queued for hourly sweep resolution):
  1. 🔴 **medium/FileDownloader hang on early EOF** — `FileDownloader.DoWork` spins forever when server closes connection before Content-Length is reached; bytesRead=0 never exits loop. Evidence: `FileDownloader.cs:112-140`. Repro: serve truncated HTTP response.
  2. 🔴 **medium/HSB equality ignores alpha but hash includes it** — Equals compares H/S/B but GetHashCode includes Alpha, violating .NET equality/hash contract. Evidence: `HSB.cs:163-166, 183-190`. Repro: `new HSB(0.1,0.2,0.3,10).Equals(new HSB(0.1,0.2,0.3,20))` returns true but hash codes differ.
  3. 🔴 **medium/Wayland active-window falls back to region selection on non-Hyprland wlroots** — `CaptureActiveWindowAsync` routes to grim+slurp which does region selection, not active window. Evidence: `WaylandCliCapture.cs:176-188, 218-255`.
  4. 🔴 **medium/Plugin assembly version pinned to 0.23.28 while app is 0.23.75** — `src/desktop/plugins/Directory.Build.props` overrides plugin version to an old value. Evidence: `Directory.Build.props` vs `plugins/Directory.Build.props`. Release hazard.
  5. 🔴 **medium/Common projects target net10.0 instead of required net10.0-windows10.0.26100.0** — `XerahS.Common.csproj` and `XerahS.Platform.Abstractions.csproj` use plain net10.0. Evidence: `XerahS.Common.csproj:2-8, Platform.Abstractions.csproj:2-7`. Build hazard.
  6. 🟡 **medium/FileDownloader refuses downloads without Content-Length** — Chunked/streaming responses have no Content-Length; body is skipped and StartDownload returns false. Evidence: `FileDownloader.cs:108-191`.
  7. 🟡 **medium/Stderr can block CLI capture helpers before timeout** — Redirected stderr on grim/slurp/etc. can fill pipe and hang capture until hard timeout. Evidence: `WaylandCliCapture.cs:70-97, 224-237, 402-417`.
  8. 🟡 **low/FFmpegDownloader cancellation token is not propagated** — `CancellationToken` accepted but not passed to network/FileDownloader calls. Evidence: `FFmpegDownloader.cs:60-65, 93-96, 134-213`.
  9. 🟡 **low/TFM mismatch (same as #5, duplicate entry via UITypeEditors feature)** — Same net10.0 vs net10.0-windows10.0.26100.0 issue found via UITypeEditors feature context.
  10. 🟡 **low/StringCollectionToStringTypeConverter silently erases non-List<string> collections** — `ConvertTo` returns string.Empty for unsupported collection types instead of delegating to base. Evidence: `StringCollectionToStringTypeConverter.cs:33-45`.
- Status: Open (queued)
- Next: Hourly sweep picks from top of queue. Follow-up areas: continue FileDownloader review, HSB equality, Wayland compositor-specific helpers.

### 2026-05-28 23:22 AWST - FileDownloader / early EOF hang on Content-Length mismatch

- Area: FileDownloader (clawpatch finding, medium severity)
- Files: `src/desktop/core/XerahS.Common/FileDownloader.cs`, `tests/XerahS.Tests/Common/FileDownloaderTests.cs`, `Directory.Build.props`
- Findings: `FileDownloader.DoWork` spins forever when server closes connection before reaching `Content-Length` — `bytesRead=0` was never checked, so the while-loop condition `DownloadedSize < FileSize` stayed true indefinitely. Added `if (bytesRead <= 0) break;` after the read, with comment. Added `FileDownloaderTestAccessor.SimulateDownloadWithEarlyEOF` for regression coverage (5 tests: partial, complete, empty, excess, exact). Note: HTTP-level early EOF is distinguishable from chunked encoding by `Content-Length` presence — when `Content-Length` is set, a 0-byte read before reaching it means premature close.
- Status: Fixed; bumped version `0.23.75` -> `0.23.76`.
- Build/test: build 0 warnings/0 errors; tests 1023+34=1057 passed, 0 failed, 1 skipped
- Commit: `a0472ad5`
- Follow-up: Continue FileDownloader review — clawpatch also flagged "refuses downloads without Content-Length" (chunked/streaming bypass) and "cancellation token not propagated to network calls". Also HSB equality/hash contract violation and plugin version pinning remain from clawpatch queue.

### 2026-05-29 05:29 AWST - HSB struct / equality hash contract violation

- Area: Common / HSB struct (clawpatch finding, medium severity)
- Files: `src/desktop/core/XerahS.Common/HSB.cs`, `tests/XerahS.Tests/Common/HSBTests.cs`, `Directory.Build.props`
- Findings: `HSB.operator ==` compared Hue, Saturation, Brightness but not Alpha, while `GetHashCode` included Alpha via `HashCode.Combine`. This violates the .NET equality/hash contract: equal objects must have equal hash codes, but two HSB instances with identical H/S/B but different alpha returned `true` for `==` yet had different hash codes.
- Fix: Added `&& (left.Alpha == right.Alpha)` to `operator ==`. Also added `internal static class TestAccessor` in HSB for test access, and added 8 regression tests covering equality, operator, and hash code contract assertions for all four fields.
- Status: Fixed; bumped version `0.23.76` -> `0.23.77`.
- Build/test: build 0 warnings/0 errors; tests 1031+34=1065 passed, 0 failed, 1 skipped
- Commit: `dc08334a`
- Follow-up: Continue clawpatch queue review: FileDownloader chunked/streaming bypass, cancellation token propagation; HSB equality follow-up items (GetHashCode optimization); Wayland active-window compositor-specific routing; plugin version pinning; TFM mismatch in Common/Platform.Abstractions.
### 2026-05-29 17:45 AWST - FileDownloader / cancellation token propagation and InvalidOperationException handling
- Area: FileDownloader
- Files: src/desktop/core/XerahS.Common/FileDownloader.cs, tests/XerahS.Tests/Common/FileDownloaderTests.cs, Directory.Build.props
- Findings: FileDownloader cancellation token was not propagated to HttpClient requests or stream operations, so StopDownload() could not reliably interrupt in-progress downloads. Also, invalid URI strings (e.g. "x") threw unhandled InvalidOperationException from HttpClient instead of returning false. Additionally, the early-exit EOF fix from prior run left IsDownloading=true when returning false early.
- Status: Fixed; bumped version `0.23.77` -> `0.23.78`.
- Build/test: build 0 warnings/0 errors; tests 1035+34=1069 passed, 0 failed, 1 skipped
- Commit: 15f5e784
- Build log: /tmp/xerahs-hourly-sweep/build-20260529-173500.log
- Test log: /tmp/xerahs-hourly-sweep/test-20260529-173500.log
- Follow-up: Continue clawpatch queue review: FileDownloader chunked/streaming bypass; Wayland active-window compositor-specific routing; plugin version pinning; TFM mismatch in Common/Platform.Abstractions. Also HSB equality follow-up items (GetHashCode optimization).

### 2026-05-29 23:47 AWST - MCP server / resource URI dispatch and uploader plugin routing review

- Area: MCP server + Uploader core / plugin routing
- Files: None (review-only pass)
- Findings: Reviewed MCP server URI dispatch: IsHistorySearchResourceUri, DecodeResourceQueryComponent (plus-encoded recovery, valid percent-encoding validation), and CreateFileUrl. All correctly implemented per prior fixes. Reviewed uploader plugin routing: ResolveAutoInstance, GetDestinationForFile, CanAddFileType, CanSetAllFileTypes, GetBlockedFileTypes, ValidateFileTypeConfiguration — all filter by IsAvailable only, matching GetDestinationForFile behavior. No fixable bugs found in either area.
- Status: Reviewed (clean)
- Build/test: build 0 warnings/0 errors; tests 1035+34=1069 passed, 0 failed, 1 skipped, logs: build-20260529-234721.log, test-20260529-234721.log
- Commit: ffd1dc65
- Follow-up: Continue clawpatch queue review: FileDownloader chunked/streaming bypass (refuses downloads without Content-Length); Wayland active-window compositor-specific routing; plugin version pinning; TFM mismatch in Common/Platform.Abstractions.
### 2026-05-30 13:58 AWST - Build/configuration / plugin assembly version alignment

- Area: Build / project configuration (clawpatch finding)
- Files: src/desktop/plugins/Directory.Build.props, Directory.Build.props
- Findings: Plugin projects (e.g. XerahS.Imgur.Plugin) were stamped with assembly version 0.23.28 while the root app is at 0.23.78, creating release metadata drift. Plugins should carry the same version as the app they ship with.
- Status: Fixed; bumped version 0.23.79 (plugins) to match root 0.23.78.
- Build/test: build 0 warnings/0 errors; tests 1035+34=1069 passed, 0 failed, 1 skipped
- Commit: 79eaffb0
- Follow-up: Continue clawpatch queue review: FileDownloader chunked/streaming bypass; Wayland active-window compositor routing; stderr redirection blocking; StringCollectionToStringTypeConverter unsupported input handling; TFM mismatch in Common/Platform.Abstractions.

### 2026-05-30 18:16 AWST - TypeConverter / StringCollectionToStringTypeConverter silent type erasure

- Area: TypeConverter / StringCollectionToStringTypeConverter
- Files: src/desktop/core/XerahS.Common/UITypeEditors/StringCollectionToStringTypeConverter.cs, tests/XerahS.Tests/Common/StringCollectionToStringTypeConverterTests.cs, Directory.Build.props
- Findings: ConvertTo silently returned string.Empty for any non-List<string> value (including string[], StringCollection, Dictionary), erasing supported types instead of delegating to base. Also returned string.Empty when destination was non-string, hiding real conversion failures.
- Status: Fixed; bumped version 0.23.79 (already staged).
- Build/test: 0 warnings/0 errors; tests 1040+34=1074 passed, 0 failed, 1 skipped; build_log=/tmp/xerahs-hourly-sweep/build-20260530-181232.log, test_log=/tmp/xerahs-hourly-sweep/test-20260530-181232.log
- Commit: 7ac362a5
- Follow-up: Continue clawpatch queue review: FileDownloader chunked/streaming bypass; Wayland active-window compositor routing; stderr redirection blocking; TFM mismatch in Common/Platform.Abstractions.
### 2026-05-31 00:27 AWST - CLI / bootstrap uploaders exit code parity

- Area: CLI / command surface
- Files: src/desktop/cli/XerahS.CLI/Commands/BootstrapCommand.cs, src/desktop/cli/XerahS.CLI/Services/CliUploaderBootstrapper.cs, Directory.Build.props
- Findings: bootstrap uploaders --json always exited 0 even when blocking issues were present (no usable uploader in a category). doctor uploaders correctly exits 1 on HasBlockingIssues, but bootstrap didn't propagate that. Fixed by calling Bootstrap directly and using report.HasBlockingIssues for exit code.
- Status: Fixed; bumped version 0.23.80 -> 0.23.81.
- Build/test: Build succeeded (0 warnings, 0 errors); tests 1040+34=1074 passed, 0 failed, 1 skipped. Logs: /tmp/xerahs-hourly-sweep/build-20260531-001752.log, /tmp/xerahs-hourly-sweep/test-20260531-001752.log
- Commit: b5163200
- Follow-up: Continue CLI review around reclip command surface, doctor uploaders --fix dry-run safety, and xerahscli upload --pipe edge cases.

### 2026-06-01 10:48 AWST - FFmpegDownloader / cancellation token propagation

- Area: FFmpegDownloader (clawpatch finding, low severity — https://github.com/KovaForge/XerahS/blob/develop/.clawpatch/reports/20260528T122646-04ea11.md fnd_sig-feat-library-0584088912-df87_78427a1c1b)
- Files: src/desktop/core/XerahS.Common/FFmpegDownloader.cs, tests/XerahS.Tests/Common/FFmpegDownloaderCancellationTests.cs, Directory.Build.props
- Findings: DownloadLatestAsync and DownloadFFprobeFallbackAsync accepted a CancellationToken parameter but did not pass it to FileDownloader.StartDownload (which supports it since v0.23.78) and only checked the token in the finally block — so cancellation was effectively a no-op. Canceled downloads would also be reported as a generic "FFmpeg download failed." message.
- Status: Fixed
- Fix: Added early IsCancellationRequested check before Directory.CreateDirectory and after the network discovery call, passed cancellationToken through to downloader.StartDownload, added post-download cancellation check, and converted OperationCanceledException + cancellation-after-exception into the user-visible "FFmpeg download was canceled." message (or null for the FFprobe fallback).
- Build/test: build 0 warnings/0 errors; tests 1046+34=1080 passed, 0 failed, 1 skipped (5 new FFmpegDownloaderCancellation tests)
- Commit: b777ea5d
- Follow-up: Continue clawpatch queue review: Wayland active-window fallback (grim+slurp does not implement active-window contract for wlroots/null desktops); Wayland stderr drainage so redirect fills don't block CLI capture; FileDownloader chunked/streaming-encoding support for responses without Content-Length.

### 2026-06-01 23:19 AWST - WaylandCliCapture / SWAY active-window fallback uses grim+focused geometry

- Area: Wayland / Linux CLI capture
- Files: src/platform/XerahS.Platform.Linux/Capture/Wayland/WaylandCliCapture.cs, src/platform/XerahS.Platform.Linux/Wayland/WindowQuery/SwayWindowPointQueryHelper.cs, tests/XerahS.Tests/Platform/Linux/WaylandWindowPointQueryHelperTests.cs, Directory.Build.props
- Findings: WaylandCliCapture.CaptureActiveWindowAsync on SWAY/null-desktop (wlroots) fell through to CaptureWithGrimSlurpAsync, which runs `slurp` with no arguments and asks the user to draw a region with the mouse — wrong UX for an "active window" capture. grimblast supports `save active` on SWAY too, but if grimblast is missing there was no focused-window fallback. On top of that, the existing SwayWindowPointQueryHelper could parse windows at a point but had no API to fetch the focused window rect.
- Fix: Added SwayWindowPointQueryHelper.TryGetFocusedWindowRectFromTreeJson (walks the Sway tree following the first `focus` child at each level until reaching a leaf) and TryGetFocusedWindowGeometryExpression (formats the rect as `grim -g` geometry `x,y WxH`). Routed the SWAY/null branch to first try grimblast `save active`, then fall back to CaptureWithSwayFocusedWindowAsync (swaymsg `-t get_tree -r` + grim `-g`). 7 regression tests covering deepest-leaf traversal, floating-node preference, malformed-rect rejection, invalid JSON, empty input, and grim geometry formatting.
- Status: Fixed
- Build/test: build 0 warnings/0 errors; tests 1057+34=1091 passed, 0 failed, 1 skipped, logs: /tmp/xerahs-hourly-sweep/build-20260601-231528.log /tmp/xerahs-hourly-sweep/test-20260601-231528.log
- Commit: 688a78ba
- Follow-up: Continue clawpatch queue review: stderr drainage for CLI capture helpers (redirected pipe fills block before timeout), remaining FileDownloader/Wayland/Hotkey edge cases.

### 2026-06-02 11:42 AWST - LinuxCliToolRunner / pipe-drain deadlock (clawpatch finding)

- Area: Linux CLI capture (clawpatch finding — wayland cli capture helpers redirect-fill)
- Files: src/platform/XerahS.Platform.Linux/Capture/Helpers/LinuxCliToolRunner.cs, tests/XerahS.Tests/Platform/Linux/LinuxCliToolRunnerTests.cs, Directory.Build.props
- Findings: LinuxCliToolRunner.RunAsync spawned a child process with RedirectStandardError=true and RedirectStandardOutput=true but never drained the pipes. The OS pipe buffer (~64KB on POSIX) could fill if the tool wrote verbose stderr/stdout, causing the child to block on the next write, the parent's WaitForExit to time out, and the capture to return a misleading null/failure even though the tool was healthy. Same anti-pattern previously fixed in MacOSClipboardService, LinuxClipboardService, and the MacOS AppleScript helpers.
- Fix: Extracted RunCoreAsync that drains stderr via process.BeginErrorReadLine and stdout via a fire-and-forget ReadToEndAsync continuation; added a bounded process.WaitForExit(1000) after Kill so the async drainers finish reading the (now-broken) pipes before the using block disposes the process. Added internal TestAccessor exposing RunForTestAsync so tests can exercise the run helper without producing a real PNG.
- Status: Fixed; bumped version 0.23.85 -> 0.23.86.
- Build/test: Build succeeded (0 warnings, 0 errors); tests 1061+34=1095 passed, 0 failed, 1 skipped (4 new LinuxCliToolRunnerTests: happy path, large-stderr 0-exit, large-stderr non-zero exit, timeout). Logs: /tmp/xerahs-hourly-sweep/build-20260602-113710.log, /tmp/xerahs-hourly-sweep/test-20260602-114023.log
- Commit: b23cb6ba
- Follow-up: Continue clawpatch queue: FileDownloader chunked/streaming-encoding support (declared-length loop wrapped the entire read/write and dropped 0-byte response bodies on chunked transfers).
### 2026-06-02 12:42 AWST - Sweep / sync-only (all fixes landed, clean build/test verified)

- Area: Sweep — sync + tracker maintenance
- Files: (none changed)
- Findings: All items from clawpatch queue have been resolved in prior runs: FileDownloader chunked/streaming (v0.23.84), HSB equality (v0.23.77), Wayland active-window/SWAY fallback (v0.23.85), LinuxCliToolRunner pipe-drain (v0.23.86), FFmpegDownloader cancellation (v0.23.83), CLI bootstrap exit code (v0.23.81), TypeConverter silent type erasure (v0.23.80), MCP server URI dispatch (v0.23.69/0.23.72), plugin version alignment (v0.23.79), onboarding dead code (v0.23.74), PluginFolderCleaner quarantine pruning (v0.23.61). Remaining follow-ups (ReClip surface, doctor uploaders --fix dry-run safety, upload --pipe edge cases) are all clean reviews — no fixable bugs found. TFM mismatch in Common/Platform.Abstractions: verified all projects use net10.0; XerahS.Platform.Abstractions is a project reference, not a TFM mismatch.
- Status: No-fix (clean review; all tracker items resolved)
- Build/test: build 0 warnings/0 errors; tests 1061+34=1095 passed, 0 failed, 1 skipped; logs: build-20260602-123841.log, test-20260602-114023.log
- Commit: (none — HEAD already at declan/develop = 7e20351a)
- Follow-up: Continue sweep queue: ReClip command surface polish, doctor uploaders --fix dry-run safety, xerahscli upload --pipe edge cases, and remaining clawpatch finding edge cases as they surface.

### 2026-06-03 00:10 AWST - OCR / OcrViewModel onboarding language-selection parity (recovered from interrupted prior session)

- Area: OCR (tool/onboarding language-selection parity)
- Files: src/desktop/app/XerahS.UI/ViewModels/OcrViewModel.cs, tests/XerahS.Tests/Tools/OcrViewModelTests.cs, Directory.Build.props
- Findings: OcrViewModel.LoadAvailableLanguages always defaulted to English even when the onboarding wizard had committed a different language to SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language. The Onboarding wizard persists the user's choice (e.g. fr) but the OCR tool's startup read-site ignored it, silently overriding the user's preference. Pattern: write-to-settings/never-read persistence parity gap.
- Fix: Read OCROptions.Language at the read site via a new private static ResolvePersistedLanguageTag() helper, trimmed once at the helper boundary (not scattered at every comparison site). Selection order: persisted (trimmed) -> English -> first available. Added 4 regression tests: PersistedOnboardingLanguage, FallsBackToEnglish_WhenPersistedLanguageUnavailable, FallsBackToFirst_WhenPersistedEmptyAndNoEnglish, TrimsPersistedWhitespaceBeforeMatching.
- Status: Fixed; bumped version 0.23.86 -> 0.23.87.
- Build/test: build 0 warnings/0 errors; tests 1065 passed, 0 failed, 1 skipped; logs: /tmp/xerahs-hourly-sweep/build-20260603-000330.log /tmp/xerahs-hourly-sweep/test-20260603-000330.log
- Commit: f30c3846 (merge commit: upstream blog conflict resolution + this fix; Declan-authored; pushed to declan/develop)
- Follow-up: Continue OCR review around the remaining onboarding-step persistence parity (SelectedOcrLanguages list -> OCROptions.PreferredLanguages design — only first language is currently carried to the runtime; document the limitation in OcrStepViewModel.StepDescription). Resume clawpatch queue: stderr-drainage for additional CLI capture helpers, remaining FileDownloader/Wayland/Hotkey edge cases, ReClip command surface polish.

### 2026-06-03 06:18 AWST - Uploader core / InstanceManager.RemoveInstance stale default cleanup logging

- Area: Uploader core / plugin routing
- Files: src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs, tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs, Directory.Build.props
- Findings: RemoveInstance removed default-instance mappings but did not call LogStaleDefaultRemoved, while the parallel GetDefaultInstance stale-cleanup path and UpdateInstance category-change path both did. Users removing an instance had no diagnostic trail in the debug log for the implicitly-cleaned default mapping, making stale-default removal harder to audit. Fix: call LogStaleDefaultRemoved(category, instanceId, "instance was removed") inside the defaults-removal loop in RemoveInstance so the diagnostic surfaces for every cleanup site. Pattern: stale-default removal logging parity across all three entry points.
- Status: Fixed; bumped version 0.23.87 -> 0.23.88.
- Build/test: 0 warnings/0 errors; XerahS.Tests 1066 passed/0 failed/1 skipped, XerahS.McpServer.Tests 34 passed/0 failed. Log paths: /tmp/xerahs-hourly-sweep/build-20260603-061438.log and /tmp/xerahs-hourly-sweep/test-20260603-061438.log.
- Commit: 6705b0e9
- Follow-up: Continue clawpatch queue: FileDownloader chunked/streaming-encoding support (still outstanding from prior sweeps), Wayland active-window compositor routing edge cases, TFM mismatch in Common/Platform.Abstractions, plugin version pinning review. Resume OCR follow-up: SelectedOcrLanguages list -> OCROptions.PreferredLanguages design (only first language carries over to runtime; document the limitation in OcrStepViewModel.StepDescription).

### 2026-06-03 18:36 AWST - MCP server / ResolveHistoryBlobPath misleading error message

- Area: MCP server
- Files:
  - src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs
  - src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs
  - Directory.Build.props
- Findings: ResolveHistoryBlobPath threw "History item thumbnail source file was not found." with item.FilePath as FileName, even when both the thumbnail and the source were missing, or when only the source was configured. The message was always "thumbnail source" which is misleading for the both-missing and source-only-missing cases. Fix: branch the message on which fields are populated — "thumbnail and source" when both are present, "source file" when only FilePath is configured, "thumbnail source file" (the original wording) when only ThumbnailURL is configured. FileName is preserved as item.FilePath to keep the prior contract for existing debug-log consumers. Callers that convert the exception to a structured response (CreateHistoryBlobMissingResponse) use item.FilePath / item.ThumbnailURL directly and are unaffected.
- Status: Fixed
- Build/test: 0 warnings, 0 errors; XerahS.Tests 1066 passed (1 skipped); XerahS.McpServer.Tests 37 passed (was 34, +3 new)
- Commit: e8c9fb14
- Follow-up: Continue clawpatch queue: stderr-drainage for additional CLI capture helpers, remaining FileDownloader / Wayland / Hotkey edge cases, and TFM mismatch in Common/Platform.Abstractions. Resume OCR follow-up to document SelectedOcrLanguages -> OCROptions.PreferredLanguages limitation in OcrStepViewModel.StepDescription (doc-only, defer to next run as a separate decision).

### 2026-06-04 00:47 AWST - MCP server / CreateHistoryDetailsAsync stale local path diagnostic

- Area: MCP server (history resource diagnostics for stale local paths)
- Files: src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs, src/tools/XerahS.McpServer.Tests/XerahSMcpServerTests.cs, Directory.Build.props
- Findings: CreateHistoryDetailsAsync silently produced null width/height, null MD5 hash, and a 0-byte file_size when item.FilePath was set but the file was not on disk (moved, deleted, or stored from a different machine). MCP clients had no way to distinguish "image is being processed" from "the stored capture path is stale", and the upload_url fallback was hidden behind a null-typed response. Made CreateHistoryDetailsAsync internal (test access via existing InternalsVisibleTo) and added explicit file_exists (bool), file_missing_path (string|null), and thumbnail_exists (bool) fields. file_exists is computed once up front; the hash/size/dimension block only runs when the source file is genuinely on disk. file_missing_path is null when FilePath is empty (no missing path to surface) and equals item.FilePath otherwise. The thumbnail_exists flag uses the same TryResolveLocalFilePath check as CreateHistoryBlobResourceUriIfLocal, keeping the two paths consistent.
- Status: Fixed; bumped version 0.23.89 -> 0.23.90.
- Build/test: build 0 warnings/0 errors; XerahS.Tests 1066 passed/0 failed/1 skipped; XerahS.McpServer.Tests 40 passed/0 failed (was 37, +3 new). Logs: /tmp/xerahs-hourly-sweep/build-20260604-004334.log, /tmp/xerahs-hourly-sweep/test-20260604-004334.log
- Commit: 27f8216c
- Follow-up: Continue MCP review around history search resource URI edge cases (already covered by the clawpatch parity pass) and resume clawpatch queue: stderr-drainage for additional CLI capture helpers, remaining FileDownloader / Wayland / Hotkey edge cases, TFM mismatch in Common/Platform.Abstractions, OCR onboarding list->OCROptions.PreferredLanguages design (only first language carries over; document the limitation in OcrStepViewModel.StepDescription).
### 2026-06-04 07:18 AWST - LinuxScreenService / xrandr capture pipe-fill + timeout-stretching deadlock

- Area: Linux platform / LinuxScreenService
- Files:
  - src/platform/XerahS.Platform.Linux/Services/LinuxScreenService.cs
  - tests/XerahS.Tests/Platform/Linux/LinuxScreenServiceTests.cs
  - Directory.Build.props
- Findings: LinuxScreenService.ParseScreens had the same ProcessStartInfo anti-pattern previously fixed in LinuxCliToolRunner (v0.23.86): RedirectStandardError=true but stderr was never drained, so a chatty xrandr (noisy EDID parsing or warnings) could fill the 64KB POSIX pipe buffer and block on its next write, hanging the parent's sync ReadToEnd. A second related bug was that stdout was read synchronously, so a child that sleeps without producing output (e.g. `sleep 5` with a 1s timeout) would stretch the call to 5 seconds before the timeout could fire. Both bugs share the same root: synchronous reads with no async drainer and no bounded wait.
- Status: Fixed; bumped version 0.23.90 -> 0.23.91.
- Build/test: build 0 warnings/0 errors. XerahS.Tests 1070 passed (was 1066, +4 new LinuxScreenServiceTests), 0 failed, 1 skipped. New tests cover happy path, 200KB stderr pipe-fill (would have hung pre-fix), sleep-with-timeout (would have stretched to 5s pre-fix), and non-zero exit code propagation. 3 pre-existing MCP server CreateHistoryDetailsAsync_* tests fail with SQLite 'disk I/O error' on this host — verified on pristine 0.23.90 HEAD without my changes, environmental, not a regression.
- Commit: 2a8c7d4a
- Logs: /tmp/xerahs-hourly-sweep/build-20260604-070552.log, /tmp/xerahs-hourly-sweep/test-full-20260604-070552.log
- Follow-up: Continue stderr-drainage audit on the remaining helpers found by the same grep: LinuxThemeService (TryReadFromGSettings, TryReadGtkThemeDark), PulseAudioHelper.RunPactl, LinuxInputService, MacOSInputService, and the WaylandCliCapture grim/slurp/grimblast paths. All share the RedirectStandardError=true + sync stdout read + WaitForExit anti-pattern. The LinuxScreenService fix is the template; same TestAccessor + RunXxxxCapture pattern applies. Resume clawpatch queue: TFM mismatch in Common/Platform.Abstractions, remaining OCR onboarding list->OCROptions.PreferredLanguages design (doc-only). Investigate the 3 pre-existing MCP SQLite 'disk I/O error' failures (likely tmp-dir file lock from a prior test run not being released).

### 2026-06-04 13:20 AWST - LinuxThemeService / gsettings pipe-fill + timeout-stretching deadlock

- Area: LinuxThemeService (Linux platform / theme detection)
- Files: src/platform/XerahS.Platform.Linux/Services/LinuxThemeService.cs, tests/XerahS.Tests/Platform/Linux/LinuxThemeServiceTests.cs, Directory.Build.props
- Findings: TryReadFromGSettings and TryReadGtkThemeDark both set RedirectStandardError=true and used sync StandardOutput.ReadToEnd() followed by WaitForExit(1000) — the same anti-pattern previously fixed in LinuxCliToolRunner (v0.23.86) and LinuxScreenService (v0.23.91). A chatty gsettings child could block writing to a full 64KB OS pipe buffer (anti-pattern A) and a child that sleeps with no stdout output could stretch a 1s timeout into the full sleep duration (anti-pattern B). Extract: introduced RunGsettingsCapture(fileName, arguments, timeoutMs) helper that drains stderr asynchronously (ReadToEndAsync + ContinueWith discard) and bounds stdout wait with Task.WaitAny(stdoutTask, Task.Delay(timeoutMs)). Added internal TestAccessor exposing RunGsettingsCapture so the test assembly can drive synthetic /bin/sh commands without a real gsettings binary. Added 4 regression tests (happy path, 200KB stderr pipe-fill, sleep-with-timeout, non-zero exit).
- Status: Fixed
- Build/test: build 0 warnings/0 errors; XerahS.Tests 1074 passed (was 1070, +4 new LinuxThemeServiceTests) / 0 failed / 1 skipped; XerahS.McpServer.Tests 37 passed / 3 failed (pre-existing SQLite 'disk I/O error' on CreateHistoryDetailsAsync_* tests — environmental, verified on pristine 0.23.90 HEAD in prior run, not a regression)
- Commit: 74652cf4
- Follow-up: Continue stderr-drainage audit on remaining helpers found by the same grep: PulseAudioHelper.RunPactl, LinuxInputService, MacOSInputService, and the WaylandCliCapture grim/slurp/grimblast paths. The LinuxScreenService (v0.23.91) and LinuxThemeService (v0.23.92) fixes are the templates. Also investigate 3 pre-existing MCP server SQLite 'disk I/O error' failures (likely tmp-dir file lock not released between test runs).

### 2026-06-04 19:37 AWST - PulseAudioHelper / RunPactl pipe-fill + timeout-stretching deadlock

- Area: LinuxPulseAudioHelper (Linux platform / audio source detection)
- Files: src/platform/XerahS.Platform.Linux/Services/PulseAudioHelper.cs, tests/XerahS.Tests/Platform/Linux/PulseAudioHelperTests.cs, Directory.Build.props
- Findings: RunPactl set RedirectStandardError=true and used the sync StandardOutput.ReadToEnd() + WaitForExit(3000) anti-pattern previously fixed in LinuxCliToolRunner (v0.23.86), LinuxScreenService (v0.23.91) and LinuxThemeService (v0.23.92). A chatty pactl child (e.g. PulseAudio debug noise on stderr) could fill the 64KB OS pipe buffer and block on its own write, causing the helper to return null on timeout (anti-pattern A). A pactl child that slept without producing stdout would stretch the 3s timeout into the full sleep duration and return exitCode=0 instead of null (anti-pattern B).
- Fix: Extracted RunPactlCapture(fileName, arguments, timeoutMs) helper that drains stderr asynchronously (ReadToEndAsync + ContinueWith discard) and bounds the stdout read with Task.WaitAny(stdoutTask, Task.Delay(timeoutMs)). After Kill, waits up to 1s for the async drainers to finish reading the (now-broken) pipes before disposing the process. Added internal TestAccessor exposing RunPactlCapture so tests can drive synthetic /bin/sh commands without a real pactl binary. RunPactl now calls RunPactlCapture("pactl", arguments, 3000) and returns the captured output (or null on timeout/spawn failure).
- Status: Fixed
- Build/test: build 0 warnings/0 errors; XerahS.Tests 1078 passed (was 1074, +4 new PulseAudioHelperTests) / 0 failed / 1 skipped; XerahS.McpServer.Tests 37 passed / 3 pre-existing SQLite 'disk I/O error' failures (environmental, verified on pristine 0.23.90 in prior run, not a regression). Logs: /tmp/xerahs-hourly-sweep/build-20260604-193331.log, /tmp/xerahs-hourly-sweep/test-20260604-193646.log
- Commit: b4e23a9f
- Follow-up: Continue stderr-drainage audit on remaining helpers: LinuxInputService.TryGetWithXdotool, MacOSInputService, and the WaylandCliCapture grim/slurp/grimblast paths. The LinuxScreenService (v0.23.91), LinuxThemeService (v0.23.92) and PulseAudioHelper (v0.23.93) fixes are the templates. Also investigate 3 pre-existing MCP server SQLite 'disk I/O error' failures (likely tmp-dir file lock not released between test runs).

### 2026-06-05 01:49 AWST - LinuxInputService / xdotool capture pipe-fill + timeout-stretching deadlock

- Area: Linux platform / LinuxInputService / TryGetWithXdotool pipe-fill + timeout-stretching deadlock
- Files: src/platform/XerahS.Platform.Linux/Services/LinuxInputService.cs, tests/XerahS.Tests/Platform/Linux/LinuxInputServiceTests.cs, Directory.Build.props
- Findings: The pre-fix TryGetWithXdotool spawned xdotool with RedirectStandardOutput + RedirectStandardError and read stdout synchronously with process.StandardOutput.ReadToEnd() before WaitForExit(500). Anti-pattern A: stderr was redirected but never drained, so a chatty xdotool (or any future code path that floods stderr) would block the child on its own write(2) to a full 64KB pipe, and the 500ms timeout would fire returning Point.Empty even though xdotool was healthy. Anti-pattern B: the synchronous ReadToEnd() blocks until the child closes its stdout pipe, so a child that sleeps with no output would stretch the call past the 500ms budget (same bug class as LinuxScreenService v0.23.91). Fix: extracted RunXdotoolCapture(fileName, arguments, timeoutMs) internal helper following the v0.23.91/0.23.92/0.23.93 template — drains stderr asynchronously via StandardError.ReadToEndAsync().ContinueWith, reads stdout asynchronously, and bounds the read with Task.WaitAny(stdoutTask, Task.Delay(timeoutMs)). After Kill() on timeout, bounded Task.WaitAll for the drainers. TryGetWithXdotool now delegates to RunXdotoolCapture("xdotool", "getmouselocation --shell", 500) and consumes the (string, int?) tuple. Manual process?.Dispose() in finally to handle multiple return paths.
- Status: Fixed
- Build/test: dotnet build Release succeeded (0 warnings, 0 errors); dotnet test XerahS.Tests 1082 passed / 1 skipped / 0 failed; new LinuxInputServiceTests: 4/4 passed (happy path, 200KB stderr pipe-fill, sleep-with-timeout asserting null exit code, non-zero exit code propagation). McpServer.Tests shows 3 pre-existing failures (CreateHistoryDetailsAsync_*: SQLite Error 10: 'disk I/O error') — environmental, documented in next_candidates and references/process-redirect-pipe-deadlock.md, not a regression in this sweep.
- Commit: 20bf0f95 (pushed to declan/develop)
- Follow-up: Continue Linux platform stderr-drainage audit — remaining: MacOSInputService (AppleScript helpers), WaylandCliCapture grim/slurp/grimblast paths (multiple Process.Start sites with RedirectStandardError/Output and sync ReadToEndAsync).Result/await without async stderr drainer). The same v0.23.91 template applies to each.
